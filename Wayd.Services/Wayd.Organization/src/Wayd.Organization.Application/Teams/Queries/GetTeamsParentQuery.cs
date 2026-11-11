using NodaTime;
using Wayd.Organization.Application.Models;

namespace Wayd.Organization.Application.Teams.Queries;

/// <summary>
/// The team of teams each of <paramref name="TeamIds"/> belongs to on <paramref name="Date"/>, keyed by team id. A
/// team belongs to at most one at a time; one in none that day is missing.
/// </summary>
public sealed record GetTeamsParentQuery(IReadOnlyCollection<Guid> TeamIds, LocalDate Date) : IQuery<IReadOnlyDictionary<Guid, TeamNavigationDto>>;

/// <summary>Handles <see cref="GetTeamsParentQuery"/>.</summary>
public sealed class GetTeamsParentQueryHandler(IOrganizationDbContext organizationDbContext)
    : IQueryHandler<GetTeamsParentQuery, IReadOnlyDictionary<Guid, TeamNavigationDto>>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, TeamNavigationDto>> Handle(GetTeamsParentQuery request, CancellationToken cancellationToken)
    {
        var teamIds = request.TeamIds.Distinct().ToList();
        if (teamIds.Count == 0)
            return new Dictionary<Guid, TeamNavigationDto>();

        var date = request.Date;
        var parents = await _organizationDbContext.BaseTeams
            .Where(t => teamIds.Contains(t.Id))
            .SelectMany(t => t.ParentMemberships)
            .Where(m => !m.IsDeleted && m.DateRange.Start <= date && (m.DateRange.End == null || date <= m.DateRange.End))
            .Select(m => new { m.SourceId, m.Target.Id, m.Target.Key, m.Target.Name, m.Target.Code, m.Target.Type })
            .ToListAsync(cancellationToken);

        return parents
            .GroupBy(p => p.SourceId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(p => new TeamNavigationDto { Id = p.Id, Key = p.Key, Name = p.Name, Code = p.Code.Value, Type = p.Type.GetDisplayName() }).First());
    }
}
