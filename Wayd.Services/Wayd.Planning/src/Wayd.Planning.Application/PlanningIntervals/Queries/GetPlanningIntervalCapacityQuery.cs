using System.Linq.Expressions;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Planning.Application.Models;
using Wayd.Planning.Application.PlanningIntervals.Dtos;

namespace Wayd.Planning.Application.PlanningIntervals.Queries;

/// <summary>
/// The capacity of each team in a planning interval, for the whole planning interval and for each of its
/// iterations. Null when there is no such planning interval, or the Team Capacity Management feature is off.
/// </summary>
public sealed record GetPlanningIntervalCapacityQuery : IQuery<PlanningIntervalCapacityDto?>
{
    public GetPlanningIntervalCapacityQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<PlanningInterval>();
    }

    public Expression<Func<PlanningInterval, bool>> IdOrKeyFilter { get; }
}

public sealed class GetPlanningIntervalCapacityQueryHandler(IPlanningDbContext planningDbContext, IDispatcher dispatcher)
    : IQueryHandler<GetPlanningIntervalCapacityQuery, PlanningIntervalCapacityDto?>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;

    public async Task<PlanningIntervalCapacityDto?> Handle(GetPlanningIntervalCapacityQuery request, CancellationToken cancellationToken)
    {
        var planningInterval = await _planningDbContext.PlanningIntervals
            .Where(request.IdOrKeyFilter)
            .Select(p => new
            {
                p.DateRange.Start,
                p.DateRange.End,
                Iterations = p.Iterations.Select(i => new { i.Id, i.Key, i.Name, i.Category, i.DateRange.Start, i.DateRange.End }).ToList(),
                Teams = p.Teams.Select(t => t.Team).ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (planningInterval is null)
            return null;

        var start = planningInterval.Start;
        var end = planningInterval.End;
        var iterations = planningInterval.Iterations.OrderBy(i => i.Start).ToList();
        var teams = planningInterval.Teams;

        // Each team's sprint days off, including those of teams beneath a team of teams, which its rollup counts.
        var teamDaysOff = await _dispatcher.Send(new GetTeamDaysOffQuery(start, end), cancellationToken);

        // The whole planning interval first, then each iteration: one read however many of either.
        List<CapacityPeriod> periods = [new(start, end), .. iterations.Select(i => new CapacityPeriod(i.Start, i.End))];
        var capacity = await _dispatcher.Send(
            new GetTeamsCapacityByPeriodQuery([.. teams.Select(t => t.Id)], periods, teamDaysOff),
            cancellationToken);
        if (capacity is null)
            return null;

        return new PlanningIntervalCapacityDto
        {
            Start = start,
            End = end,
            Iterations = [.. iterations.Select(i => new PlanningIntervalCapacityIterationDto
            {
                Id = i.Id,
                Key = i.Key,
                Name = i.Name,
                Category = SimpleNavigationDto.FromEnum(i.Category),
                Start = i.Start,
                End = i.End,
            })],
            Teams = [.. teams
                .Where(t => capacity.ContainsKey(t.Id))
                .OrderBy(t => t.Type == TeamType.TeamOfTeams)
                .ThenBy(t => t.Code, StringComparer.OrdinalIgnoreCase)
                .Select(team =>
                {
                    // One entry per period, in the order asked for: the whole planning interval, then each iteration.
                    var byPeriod = capacity[team.Id];
                    return new PlanningIntervalTeamCapacityDto
                    {
                        Team = PlanningTeamNavigationDto.FromPlanningTeam(team),
                        IsActive = team.IsActive,
                        HasAllocations = byPeriod[0].HasAllocations,
                        Total = Figures(byPeriod[0]),
                        Iterations = [.. byPeriod.Skip(1).Select(Figures)],
                        TeamDaysOff = team.Type == TeamType.Team ? teamDaysOff.GetValueOrDefault(team.Id, []) : [],
                    };
                })],
        };
    }

    private static PlanningIntervalCapacityFiguresDto Figures(TeamPeriodCapacity capacity) => new()
    {
        AllocatedPersonDays = capacity.AllocatedPersonDays,
        DeliveryPersonDays = capacity.DeliveryPersonDays,
        AwayPersonDays = capacity.AwayPersonDays,
    };
}
