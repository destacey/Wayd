using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkTeams.Dtos;
using Wayd.Work.Domain.Models.Forecasting;

namespace Wayd.Work.Application.WorkItems.Forecasting;

/// <summary>
/// Forecasts when a set of work items will all be done, from their teams' throughput, their
/// positions in those teams' backlogs, and the open predecessors they wait on. A portfolio work
/// item stands for its open backlog descendants.
/// </summary>
internal sealed class WorkItemForecastBuilder(IWorkDbContext workDbContext)
{
    private static readonly int[] _confidenceLevels = [50, 70, 85, 95];

    private readonly IWorkDbContext _workDbContext = workDbContext;

    /// <param name="workItemIds">The work items to forecast. Done and removed ones contribute nothing.</param>
    /// <param name="seed">
    /// Identifies what is being forecast; with the date, it seeds the simulation so the same
    /// inputs on the same day give the same forecast on every request.
    /// </param>
    public async Task<WorkItemForecastDto> Build(
        IReadOnlyCollection<Guid> workItemIds,
        Guid seed,
        Instant now,
        LocalDate? targetDate,
        ForecastOptions options,
        CancellationToken cancellationToken)
    {
        Guard.Against.Null(workItemIds);
        Guard.Against.Null(options);

        var (from, to) = TeamThroughputSampler.LookbackWindow(now, options.LookbackDays);
        var start = to.PlusDays(1);

        var roots = await _workDbContext.WorkItems
            .Where(w => workItemIds.Contains(w.Id))
            .Select(w => new
            {
                w.Id,
                w.StatusCategory,
                Tier = w.Type.Level != null ? w.Type.Level.Tier : (WorkTypeTier?)null,
            })
            .ToListAsync(cancellationToken);

        var loader = new ForecastNetworkLoader(_workDbContext);
        var openRoots = roots.Where(r => r.StatusCategory is WorkStatusCategory.Proposed or WorkStatusCategory.Active).ToList();
        var portfolioRoots = openRoots.Where(r => r.Tier == WorkTypeTier.Portfolio).Select(r => r.Id).ToList();

        var remaining = openRoots.Where(r => r.Tier != WorkTypeTier.Portfolio).Select(r => r.Id).ToHashSet();
        if (portfolioRoots.Count > 0)
            remaining.UnionWith(await loader.OpenBacklogDescendants(portfolioRoots, cancellationToken));

        if (remaining.Count == 0)
        {
            var outcome = roots.Count > 0 && roots.All(r => r.StatusCategory == WorkStatusCategory.Done)
                ? WorkItemForecastOutcome.AlreadyDone
                : WorkItemForecastOutcome.NoRemainingWork;
            return new WorkItemForecastDto
            {
                Outcome = SimpleNavigationDto.FromEnum(outcome),
                ForecastStart = start,
                LookbackDays = options.LookbackDays,
                IgnoreDependencies = options.IgnoreDependencies,
                StartedWorkFirst = options.StartedWorkFirst,
                TargetDate = targetDate,
            };
        }

        var network = await loader.Load(remaining, options, cancellationToken);

        var teamIds = network.BacklogPositions.Keys
            .Select(id => network.Items[id].TeamId!.Value)
            .Distinct()
            .Order()
            .ToList();
        var samples = await new TeamThroughputSampler(_workDbContext).Sample(teamIds, from, to, cancellationToken);

        var issues = new List<(Guid WorkItemId, ForecastIssueType Type)>();

        // Items in a dependency need forecasts of their own, trial by trial. The rest only matter
        // to the combined date, and within one team's simulated run an item further down the
        // backlog never finishes earlier, so only the furthest-down of them on each team is
        // simulated. The furthest position overall is unchanged, so are the draws.
        var linked = network.Dependencies.SelectMany(d => new[] { d.Predecessor, d.Successor }).ToHashSet();
        var ownForecasts = linked.ToDictionary(id => id, _ => (CompletionForecast?)null);
        var unlinkedLatest = new Dictionary<Guid, CompletionForecast>();
        var hasOwnForecast = new HashSet<Guid>();

        foreach (var item in network.Items.Values)
        {
            var issue = item switch
            {
                { IsBacklogItem: false } => ForecastIssueType.NotABacklogItem,
                { TeamId: null } => ForecastIssueType.NoTeam,
                _ => (ForecastIssueType?)null,
            };
            if (issue.HasValue)
                issues.Add((item.Id, issue.Value));
        }

        // One random source, used team by team in a fixed order, so the draws are reproducible.
        var random = new Random(ForecastSeed.From(seed, start));
        foreach (var teamId in teamIds)
        {
            var teamItems = network.BacklogPositions
                .Where(p => network.Items[p.Key].TeamId == teamId)
                .OrderBy(p => p.Key)
                .ToList();

            if (!TeamThroughputSampler.HasEnoughHistory(samples[teamId]))
            {
                issues.AddRange(teamItems.Select(p => (p.Key, ForecastIssueType.NotEnoughHistory)));
                continue;
            }

            hasOwnForecast.UnionWith(teamItems.Select(p => p.Key));

            var simulated = teamItems.Where(p => linked.Contains(p.Key)).ToList();
            var unlinked = teamItems.Where(p => !linked.Contains(p.Key)).ToList();
            if (unlinked.Count > 0)
                simulated.Add(unlinked.MaxBy(p => p.Value));

            var forecasts = MonteCarloForecaster.ForecastBacklogPositions(samples[teamId], [.. simulated.Select(p => (double)p.Value)], random);
            foreach (var (item, forecast) in simulated.Zip(forecasts))
            {
                if (linked.Contains(item.Key))
                    ownForecasts[item.Key] = forecast;
                else
                    unlinkedLatest[teamId] = forecast;
            }
        }

        var result = DependencyForecaster.Apply(ownForecasts, network.Dependencies);

        var forecastable = remaining
            .Where(id => linked.Contains(id) ? result.Items[id].Forecast is not null : hasOwnForecast.Contains(id))
            .ToList();
        var combined = forecastable.Count == 0
            ? null
            : CompletionForecast.LatestOf(
            [
                .. forecastable.Where(linked.Contains).Select(id => result.Items[id].Forecast!),
                .. unlinkedLatest.Values,
            ]);

        var finalOutcome = combined is not null
            ? WorkItemForecastOutcome.Forecast
            : remaining.Select(id => OutcomeOf(id, hasOwnForecast, issues)).Distinct().ToList() is [var shared]
                ? shared
                : WorkItemForecastOutcome.CannotForecast;

        var teams = await _workDbContext.WorkTeams
            .Where(t => teamIds.Contains(t.Id))
            .ProjectToType<WorkTeamNavigationDto>()
            .ToListAsync(cancellationToken);

        return new WorkItemForecastDto
        {
            Outcome = SimpleNavigationDto.FromEnum(finalOutcome),
            ForecastStart = start,
            LookbackDays = options.LookbackDays,
            IgnoreDependencies = options.IgnoreDependencies,
            StartedWorkFirst = options.StartedWorkFirst,
            BacklogPosition = remaining.Count == 1 && network.BacklogPositions.TryGetValue(remaining.Single(), out var position) ? position : null,
            RemainingWorkItems = remaining.Count,
            ExcludedWorkItems = combined is null ? [] : [.. remaining.Except(forecastable).Select(id => ToDto(network.Items[id]))],
            Teams = [.. teams.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).Select(t => new ForecastTeamDto
            {
                Team = t,
                From = from,
                To = to,
                ItemsCompleted = (int)samples[t.Id].Total,
            })],
            TargetDate = targetDate,
            ChanceOfFinishingByTargetDate = combined is not null && targetDate.HasValue
                ? combined.ShareFinishedWithin(Period.DaysBetween(start, targetDate.Value) + 1)
                : null,
            Trials = combined?.Trials ?? 0,
            TrialsBeyondHorizon = combined?.TrialsBeyondHorizon ?? 0,
            Percentiles = combined is null ? [] : [.. _confidenceLevels.Select(p => new ForecastPercentileDto
            {
                Confidence = p,
                Date = combined.DaysAtConfidence(p) is int days ? DateOfDay(start, days) : null,
            })],
            Histogram = combined is null ? [] : [.. combined.FinishedTrialDays
                .GroupBy(days => days)
                .Select(g => new ForecastHistogramBucketDto { Date = DateOfDay(start, g.Key), Trials = g.Count() })],
            Dependencies = [.. result.Dependencies.Select(d => new ForecastDependencyInfluenceDto
            {
                Predecessor = ToDto(network.Items[d.Predecessor]),
                Successor = ToDto(network.Items[d.Successor]),
                ShareOfTrialsSettingFinish = d.ShareOfTrialsSettingFinish,
            })],
            IgnoredDependencies =
            [
                .. result.IgnoredDependencies.Select(d => new ForecastDependencyLinkDto
                {
                    Predecessor = ToDto(network.Items[d.Predecessor]),
                    Successor = ToDto(network.Items[d.Successor]),
                    Reason = SimpleNavigationDto.FromEnum(IgnoredDependencyReason.ClosesCycle),
                }),
                .. network.RemovedPredecessorDependencies.Select(d => new ForecastDependencyLinkDto
                {
                    Predecessor = ToDto(d.Predecessor),
                    Successor = ToDto(d.Successor),
                    Reason = SimpleNavigationDto.FromEnum(IgnoredDependencyReason.PredecessorRemoved),
                }),
            ],
            Issues = [.. issues.Distinct().Select(i => new ForecastIssueDto
            {
                WorkItem = ToDto(network.Items[i.WorkItemId]),
                Type = SimpleNavigationDto.FromEnum(i.Type),
            })],
        };
    }

    /// <summary>
    /// Why a remaining work item has no forecast after dependencies were applied.
    /// </summary>
    private static WorkItemForecastOutcome OutcomeOf(
        Guid workItemId,
        HashSet<Guid> hasOwnForecast,
        List<(Guid WorkItemId, ForecastIssueType Type)> issues)
        => hasOwnForecast.Contains(workItemId) ? WorkItemForecastOutcome.BlockedByDependency
            : issues.Contains((workItemId, ForecastIssueType.NotEnoughHistory)) ? WorkItemForecastOutcome.NotEnoughHistory
            : WorkItemForecastOutcome.CannotForecast;

    /// <summary>
    /// Day 1 is the forecast's first simulated day.
    /// </summary>
    private static LocalDate DateOfDay(LocalDate start, int day) => start.PlusDays(Math.Max(day, 1) - 1);

    private static ForecastWorkItemDto ToDto(ForecastNetworkItem item) => new()
    {
        Id = item.Id,
        Key = item.Key.Value,
        WorkspaceKey = item.Key.WorkspaceKey,
        Title = item.Title,
    };
}
