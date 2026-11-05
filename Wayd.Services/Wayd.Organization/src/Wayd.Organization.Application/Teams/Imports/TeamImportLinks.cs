using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Imports;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Organization.Application.Persistence;

namespace Wayd.Organization.Application.Teams.Imports;

/// <summary>
/// Turns what the team imports record into links: a team or team of teams by its own id, and a hierarchy
/// membership by the team it placed, since a membership has no page of its own.
/// </summary>
internal static class TeamImportLinks
{
    /// <summary>Links each id that names a team or a team of teams.</summary>
    public static async Task<IReadOnlyDictionary<Guid, ImportRecordLink>> ForTeams(
        IOrganizationDbContext organizationDbContext,
        IReadOnlyCollection<Guid> teamIds,
        CancellationToken cancellationToken) =>
        await organizationDbContext.BaseTeams
            .AsNoTracking()
            .Where(t => teamIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Key, t.Name, t.Type })
            .ToDictionaryAsync(t => t.Id, t => Link(t.Type, t.Key, t.Name), cancellationToken);

    /// <summary>Links each membership id to the child team the membership placed under its parent.</summary>
    public static async Task<IReadOnlyDictionary<Guid, ImportRecordLink>> ForMemberships(
        IOrganizationDbContext organizationDbContext,
        IReadOnlyCollection<Guid> membershipIds,
        CancellationToken cancellationToken) =>
        await organizationDbContext.BaseTeams
            .AsNoTracking()
            .SelectMany(t => t.ParentMemberships
                .Where(m => membershipIds.Contains(m.Id))
                .Select(m => new { m.Id, t.Key, t.Name, t.Type }))
            .ToDictionaryAsync(m => m.Id, m => Link(m.Type, m.Key, m.Name), cancellationToken);

    private static ImportRecordLink Link(TeamType type, int key, string name) => new(
        type == TeamType.TeamOfTeams ? ImportRecordType.TeamOfTeams : ImportRecordType.Team,
        key.ToString(CultureInfo.InvariantCulture),
        name);
}
