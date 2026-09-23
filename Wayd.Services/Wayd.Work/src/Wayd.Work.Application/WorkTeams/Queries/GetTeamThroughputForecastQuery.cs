using System.Linq.Expressions;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Models.Organizations;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkItems.Forecasting;
using Wayd.Work.Application.WorkTeams.Dtos;
using Wayd.Work.Domain.Models.Forecasting;

namespace Wayd.Work.Application.WorkTeams.Queries;

/// <summary>
/// How many backlog work items a team will finish by a date, from its recent throughput.
/// </summary>
/// <param name="LookbackDays">Days of history to sample the team's throughput from.</param>
public sealed record GetTeamThroughputForecastQuery(
    TeamIdOrCode TeamIdOrCode,
    LocalDate TargetDate,
    int LookbackDays = ForecastOptions.DefaultLookbackDays) : IQuery<Result<TeamThroughputForecastDto?>>;

public sealed class GetTeamThroughputForecastQueryValidator : AbstractValidator<GetTeamThroughputForecastQuery>
{
    public GetTeamThroughputForecastQueryValidator()
    {
        RuleFor(q => q.LookbackDays)
            .InclusiveBetween(ForecastOptions.MinLookbackDays, ForecastOptions.MaxLookbackDays)
            .WithMessage($"The history window must be between {ForecastOptions.MinLookbackDays} and {ForecastOptions.MaxLookbackDays} days.");
    }
}

public sealed class GetTeamThroughputForecastQueryHandler(
    IWorkDbContext workDbContext,
    IDateTimeProvider dateTimeProvider) : IQueryHandler<GetTeamThroughputForecastQuery, Result<TeamThroughputForecastDto?>>
{
    private static readonly int[] _confidenceLevels = [50, 70, 85, 95];

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result<TeamThroughputForecastDto?>> Handle(GetTeamThroughputForecastQuery request, CancellationToken cancellationToken)
    {
        var team = await _workDbContext.WorkTeams
            .Where(request.TeamIdOrCode.Match<Expression<Func<WorkTeam, bool>>>(
                id => t => t.Id == id,
                code => t => t.Code == code))
            .ProjectToType<WorkTeamNavigationDto>()
            .FirstOrDefaultAsync(cancellationToken);

        if (team is null)
            return Result.Success<TeamThroughputForecastDto?>(null);

        var (from, to) = TeamThroughputSampler.LookbackWindow(_dateTimeProvider.Now, request.LookbackDays);
        var start = to.PlusDays(1);
        var days = Period.DaysBetween(start, request.TargetDate) + 1;

        if (days < 1)
            return Result.Failure<TeamThroughputForecastDto?>("The target date must be today or later.");
        if (days > MonteCarloForecaster.DefaultHorizonDays)
            return Result.Failure<TeamThroughputForecastDto?>($"The target date must be within {MonteCarloForecaster.DefaultHorizonDays} days.");

        var sample = (await new TeamThroughputSampler(_workDbContext).Sample([team.Id], from, to, cancellationToken))[team.Id];
        var backlog = await new ForecastNetworkLoader(_workDbContext).TeamBacklog(team.Id, cancellationToken);

        var forecastTeam = new ForecastTeamDto { Team = team, From = from, To = to, ItemsCompleted = (int)sample.Total };

        if (!TeamThroughputSampler.HasEnoughHistory(sample))
        {
            return new TeamThroughputForecastDto
            {
                Outcome = SimpleNavigationDto.FromEnum(WorkItemForecastOutcome.NotEnoughHistory),
                Team = forecastTeam,
                ForecastStart = start,
                TargetDate = request.TargetDate,
                LookbackDays = request.LookbackDays,
                Days = days,
                BacklogWorkItems = backlog.Count,
            };
        }

        var forecast = MonteCarloForecaster.ForecastThroughput(sample, days, new Random(ForecastSeed.From(team.Id, start)));

        return new TeamThroughputForecastDto
        {
            Outcome = SimpleNavigationDto.FromEnum(WorkItemForecastOutcome.Forecast),
            Team = forecastTeam,
            ForecastStart = start,
            TargetDate = request.TargetDate,
            LookbackDays = request.LookbackDays,
            Days = days,
            BacklogWorkItems = backlog.Count,
            Trials = forecast.Trials,
            Percentiles = [.. _confidenceLevels.Select(p =>
            {
                var workItems = (int)forecast.AmountAtConfidence(p);
                return new ThroughputPercentileDto
                {
                    Confidence = p,
                    WorkItems = workItems,
                    ThroughWorkItem = workItems > 0 && workItems <= backlog.Count ? ToDto(backlog[workItems - 1]) : null,
                };
            })],
            Histogram = [.. forecast.TrialTotals
                .GroupBy(total => (int)total)
                .Select(g => new ThroughputHistogramBucketDto { WorkItems = g.Key, Trials = g.Count() })],
        };
    }

    private static ForecastWorkItemDto ToDto(ForecastBacklogEntry item) => new()
    {
        Id = item.Id,
        Key = item.Key.Value,
        WorkspaceKey = item.Key.WorkspaceKey,
        Title = item.Title,
    };
}
