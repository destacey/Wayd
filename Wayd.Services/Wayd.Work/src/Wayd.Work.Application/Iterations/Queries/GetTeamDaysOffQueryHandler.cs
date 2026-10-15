using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Iterations.Queries;

public sealed class GetTeamDaysOffQueryHandler(IWorkDbContext workDbContext)
    : IQueryHandler<GetTeamDaysOffQuery, IReadOnlyDictionary<Guid, IReadOnlyList<LocalDate>>>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<LocalDate>>> Handle(GetTeamDaysOffQuery request, CancellationToken cancellationToken)
    {
        var from = request.From;
        var to = request.To;

        // Every team's, not only those a caller names: a team of teams' rollup counts the teams beneath it, which
        // only Organization knows. Only the team and the stored days are read, not the whole sprint.
        var sprints = await _workDbContext.Iterations
            .Where(i => i.Type == IterationType.Sprint && i.TeamId != null)
            .Where(i => i.DateRange.Start <= to && i.DateRange.End >= from)
            .Select(i => new { TeamId = i.TeamId!.Value, DaysOff = EF.Property<List<LocalDate>>(i, "_teamDaysOff") })
            .ToListAsync(cancellationToken);

        return sprints
            .Where(s => s.DaysOff.Count > 0)
            .SelectMany(s => s.DaysOff.Where(d => from <= d && d <= to), (s, day) => (s.TeamId, Day: day))
            .GroupBy(d => d.TeamId)
            .ToDictionary(
                team => team.Key,
                team => (IReadOnlyList<LocalDate>)[.. team.Select(d => d.Day).Distinct().Order()]);
    }
}
