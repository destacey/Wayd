using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Iterations.Queries;

public sealed class GetTeamDaysOffQueryHandler(IWorkDbContext workDbContext)
    : IQueryHandler<GetTeamDaysOffQuery, IReadOnlyDictionary<Guid, IReadOnlyList<TeamDayOff>>>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<TeamDayOff>>> Handle(GetTeamDaysOffQuery request, CancellationToken cancellationToken)
    {
        var from = request.From;
        var to = request.To;

        // Every team's, not only those a caller names: a team of teams' rollup counts the teams beneath it, which
        // only Organization knows. Only the team, the dates and the stored days are read, not the whole sprint.
        var sprints = await _workDbContext.Iterations
            .Where(i => i.Type == IterationType.Sprint && i.TeamId != null)
            .Where(i => i.DateRange.Start <= to && i.DateRange.End >= from)
            .Select(i => new
            {
                TeamId = i.TeamId!.Value,
                Start = i.DateRange.Start!.Value,
                End = i.DateRange.End!.Value,
                DaysOff = EF.Property<List<TeamDayOff>>(i, "_teamDaysOff"),
            })
            .ToListAsync(cancellationToken);

        // A day the source has since moved the sprint's dates away from is no longer the sprint's, as the
        // sprint itself reads it.
        return sprints
            .Where(s => s.DaysOff.Count > 0)
            .SelectMany(
                s => s.DaysOff.Where(d => from <= d.Date && d.Date <= to && s.Start <= d.Date && d.Date <= s.End),
                (s, day) => (s.TeamId, Day: day))
            .GroupBy(d => d.TeamId)
            .ToDictionary(
                team => team.Key,
                team => (IReadOnlyList<TeamDayOff>)[.. team
                    .Select(d => d.Day)
                    .GroupBy(d => d.Date)
                    .Select(day => day.MaxBy(d => d.PercentOff)!)
                    .OrderBy(d => d.Date)]);
    }
}
