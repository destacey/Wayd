using System.Linq.Expressions;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Models.Organizations;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Common.Domain.Settings;
using Wayd.Work.Application.Iterations.Sprints;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItems.Forecasting;
using Wayd.Work.Application.WorkTeams.Dtos;
using Wayd.Work.Domain.Models.BacklogHealth;

namespace Wayd.Work.Application.WorkTeams.Queries;

/// <summary>
/// A team's backlog graded against <paramref name="Thresholds"/>.
/// </summary>
/// <param name="LookbackDays">Days of history to measure throughput, cycle time and net flow over.</param>
public sealed record GetTeamBacklogHealthQuery(
    TeamIdOrCode TeamIdOrCode,
    BacklogHealthThresholds Thresholds,
    int LookbackDays = ForecastOptions.DefaultLookbackDays) : IQuery<Result<TeamBacklogHealthDto?>>;

public sealed class GetTeamBacklogHealthQueryValidator : AbstractValidator<GetTeamBacklogHealthQuery>
{
    public GetTeamBacklogHealthQueryValidator()
    {
        RuleFor(q => q.LookbackDays)
            .InclusiveBetween(ForecastOptions.MinLookbackDays, ForecastOptions.MaxLookbackDays)
            .WithMessage($"The history window must be between {ForecastOptions.MinLookbackDays} and {ForecastOptions.MaxLookbackDays} days.");

        RuleFor(q => q.Thresholds)
            .NotNull()
            .SetValidator(new BacklogHealthThresholdsValidator());
    }
}

public sealed class BacklogHealthThresholdsValidator : AbstractValidator<BacklogHealthThresholds>
{
    public BacklogHealthThresholdsValidator()
    {
        RuleFor(t => t.StaleDays).InclusiveBetween(1, 3650);
        RuleFor(t => t.OldProposedDays).InclusiveBetween(1, 3650);
        RuleFor(t => t.AgingWipPercentile).InclusiveBetween(1, 100);
        RuleFor(t => t.OversizedPercentile).InclusiveBetween(1, 100);
        RuleFor(t => t.ReadinessWindowWeeks).InclusiveBetween(1, 52);
        RuleFor(t => t.ReadinessFallbackItems).InclusiveBetween(1, 1000);

        RuleFor(t => t.AtRiskPercent).InclusiveBetween(1, 100);
        RuleFor(t => t.UnhealthyPercent).InclusiveBetween(1, 100)
            .GreaterThanOrEqualTo(t => t.AtRiskPercent)
            .WithMessage("The Unhealthy percent must not be below the At Risk percent.");

        RuleFor(t => t.RunwayUnhealthyWeeks).InclusiveBetween(0, 520);
        RuleFor(t => t.RunwayAtRiskWeeks).InclusiveBetween(0, 520)
            .GreaterThanOrEqualTo(t => t.RunwayUnhealthyWeeks)
            .WithMessage("The At Risk runway must not be shorter than the Unhealthy runway.");
        RuleFor(t => t.RunwayTooLongWeeks).InclusiveBetween(1, 520)
            .GreaterThan(t => t.RunwayAtRiskWeeks)
            .WithMessage("The too-long runway must be longer than the At Risk runway.");

        RuleFor(t => t.NetFlowAtRisk).InclusiveBetween(0.01, 100);
        RuleFor(t => t.NetFlowUnhealthy).InclusiveBetween(0.01, 100)
            .GreaterThanOrEqualTo(t => t.NetFlowAtRisk)
            .WithMessage("The Unhealthy net flow must not be below the At Risk net flow.");

        RuleFor(t => t.WipLoadAtRisk).InclusiveBetween(0.01, 100);
        RuleFor(t => t.WipLoadUnhealthy).InclusiveBetween(0.01, 100)
            .GreaterThanOrEqualTo(t => t.WipLoadAtRisk)
            .WithMessage("The Unhealthy WIP load must not be below the At Risk WIP load.");
    }
}

public sealed class GetTeamBacklogHealthQueryHandler(
    IWorkDbContext workDbContext,
    IDispatcher dispatcher,
    ISettings<SchedulingSettings> schedulingSettings,
    IDateTimeProvider dateTimeProvider) : IQueryHandler<GetTeamBacklogHealthQuery, Result<TeamBacklogHealthDto?>>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result<TeamBacklogHealthDto?>> Handle(GetTeamBacklogHealthQuery request, CancellationToken cancellationToken)
    {
        var team = await _workDbContext.WorkTeams
            .Where(request.TeamIdOrCode.Match<Expression<Func<WorkTeam, bool>>>(
                id => t => t.Id == id,
                code => t => t.Code == code))
            .ProjectToType<WorkTeamNavigationDto>()
            .FirstOrDefaultAsync(cancellationToken);

        if (team is null)
            return Result.Success<TeamBacklogHealthDto?>(null);

        var now = _dateTimeProvider.Now;
        var (from, to) = TeamThroughputSampler.LookbackWindow(now, request.LookbackDays);
        var start = from.AtStartOfDayInZone(DateTimeZone.Utc).ToInstant();
        var end = to.PlusDays(1).AtStartOfDayInZone(DateTimeZone.Utc).ToInstant();

        // The backlog is measured now, so in the sizing method the team uses today (UTC, as the window is).
        var schedule = await _dispatcher.Send(new GetTeamScheduleQuery(team.Id, now.InUtc().Date), cancellationToken);
        var sizingMethod = schedule?.SizingMethod ?? SizingMethod.Count;
        double? EstimateOf(double? storyPoints, double? effort, double? size) =>
            sizingMethod == SizingMethod.Count ? null : WorkItemEstimate.Of(sizingMethod, storyPoints, effort, size);

        var teamBacklogItems = _workDbContext.WorkItems
            .Where(w => w.TeamId == team.Id)
            .Where(w => w.Type.Level!.Tier == WorkTypeTier.Requirement);

        var openItems = teamBacklogItems
            .Where(w => w.StatusCategory == WorkStatusCategory.Proposed || w.StatusCategory == WorkStatusCategory.Active);

        // Parent and Iteration are read through navigations, which the in-memory fakes leave unset.
        var facts = await openItems
            .Select(w => new
            {
                w.Id,
                w.StackRank,
                w.StatusCategory,
                w.StoryPoints,
                w.Effort,
                w.Size,
                w.Created,
                w.LastModified,
                w.ActivatedTimestamp,
                IsAssigned = w.AssignedToId.HasValue,
                HasParent = w.ParentId.HasValue,
                IsParentClosed = w.Parent != null
                    && (w.Parent.StatusCategory == WorkStatusCategory.Done || w.Parent.StatusCategory == WorkStatusCategory.Removed),
                HasProject = w.ProjectId.HasValue || w.ParentProjectId.HasValue,
                Sprint = w.Iteration != null && w.Iteration.Type == IterationType.Sprint
                    ? new { w.Iteration.Id, w.Iteration.TeamId, w.Iteration.DateRange.Start, w.Iteration.DateRange.End }
                    : null,
            })
            .ToListAsync(cancellationToken);

        // An item's sprint can belong to another team, so each sprint is read on its own team's timeline.
        var sprintStates = await _workDbContext.LoadIterationStateReader(
            _dispatcher,
            _schedulingSettings,
            facts.Where(f => f.Sprint?.TeamId is not null).Select(f => f.Sprint!.TeamId!.Value),
            now,
            cancellationToken);

        var openIds = facts.Select(f => f.Id).ToList();
        var predecessors = (await _workDbContext.WorkItemDependencies
                .Where(d => d.RemovedOn == null && openIds.Contains(d.TargetId))
                .Select(d => new { d.SourceId, d.TargetId })
                .ToListAsync(cancellationToken))
            .ToLookup(d => d.TargetId, d => d.SourceId);

        // Ordered as ForecastNetworkLoader orders a team backlog, so ranks agree with the forecast's.
        var backlog = facts
            .OrderBy(f => f.StackRank).ThenBy(f => f.Created).ThenBy(f => f.Id)
            .Select((f, index) => new BacklogHealthItem
            {
                Id = f.Id,
                Rank = index + 1,
                StatusCategory = f.StatusCategory,
                Estimate = EstimateOf(f.StoryPoints, f.Effort, f.Size),
                Created = f.Created,
                LastModified = f.LastModified,
                Activated = f.ActivatedTimestamp,
                IsAssigned = f.IsAssigned,
                HasParent = f.HasParent,
                IsParentClosed = f.IsParentClosed,
                HasProject = f.HasProject,
                SprintState = f.Sprint is { } sprint
                    ? sprintStates.StateOf(sprint.Id, new IterationDateRange(sprint.Start, sprint.End))
                    : null,
                PredecessorIds = [.. predecessors[f.Id]],
            })
            .ToList();

        // Removed items also carry a DoneTimestamp, so the status category is what excludes them.
        var completions = (await teamBacklogItems
                .Where(w => w.StatusCategory == WorkStatusCategory.Done)
                .Where(w => w.DoneTimestamp >= start && w.DoneTimestamp < end)
                .Select(w => new
                {
                    w.ActivatedTimestamp,
                    Done = w.DoneTimestamp!.Value,
                    w.StoryPoints,
                    w.Effort,
                    w.Size,
                    HasProject = w.ProjectId.HasValue || w.ParentProjectId.HasValue,
                })
                .ToListAsync(cancellationToken))
            .Select(c => new
            {
                Completion = new BacklogHealthCompletion(c.ActivatedTimestamp, c.Done, EstimateOf(c.StoryPoints, c.Effort, c.Size)),
                c.HasProject,
            })
            .ToList();

        // Removed items are left out as completions are, so triaging work away does not read as growth.
        var itemsCreated = await teamBacklogItems
            .Where(w => w.StatusCategory != WorkStatusCategory.Removed)
            .CountAsync(w => w.Created >= start && w.Created < end, cancellationToken);

        var memberCount = await _dispatcher.Send(new GetTeamMemberCountQuery(team.Id), cancellationToken);

        var usesProjects = backlog.Any(i => i.HasProject) || completions.Any(c => c.HasProject);

        var assessment = BacklogHealthAssessor.Assess(
            backlog,
            new BacklogHealthHistory(request.LookbackDays, [.. completions.Select(c => c.Completion)], itemsCreated),
            sizingMethod,
            memberCount,
            usesProjects,
            now,
            request.Thresholds);

        var rankById = backlog.ToDictionary(i => i.Id, i => i.Rank);
        var estimateById = backlog.ToDictionary(i => i.Id, i => i.Estimate);

        // A sync between the two reads can open an item that was not assessed; it is left out.
        var workItems = (await openItems
                .ProjectToType<BacklogHealthWorkItemDto>()
                .ToListAsync(cancellationToken))
            .Where(w => rankById.ContainsKey(w.Id))
            .ToList();

        foreach (var workItem in workItems)
        {
            workItem.Rank = rankById[workItem.Id];
            workItem.Estimate = estimateById[workItem.Id];
            workItem.Flags = [.. assessment.ItemFlags[workItem.Id].Select(c => SimpleNavigationDto.FromEnum(c))];
        }

        return new TeamBacklogHealthDto
        {
            Team = team,
            Thresholds = assessment.Thresholds,
            LookbackDays = request.LookbackDays,
            From = from,
            To = to,
            SizingMethod = sizingMethod,
            TotalWorkItems = backlog.Count,
            TotalEstimate = sizingMethod == SizingMethod.Count ? backlog.Count : backlog.Sum(i => i.Estimate ?? 0),
            ProposedWorkItems = backlog.Count(i => i.StatusCategory == WorkStatusCategory.Proposed),
            ActiveWorkItems = backlog.Count(i => i.StatusCategory == WorkStatusCategory.Active),
            ItemsCompleted = completions.Count,
            ItemsCreated = itemsCreated,
            MinimumItemsCompleted = BacklogHealthAssessor.MinimumItemsCompleted,
            MemberCount = memberCount,
            ReadinessWindowWorkItems = assessment.ReadinessWindowItems,
            AgingWipDays = assessment.AgingWipDays,
            OversizedEstimate = assessment.OversizedEstimate,
            Checks = assessment.Checks.Adapt<List<BacklogHealthCheckDto>>(),
            WorkItems = [.. workItems.OrderBy(w => w.Rank)],
        };
    }
}
