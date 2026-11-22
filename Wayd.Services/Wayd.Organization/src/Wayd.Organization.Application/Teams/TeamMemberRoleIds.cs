namespace Wayd.Organization.Application.Teams;

/// <summary>
/// Checks the roles a team member is given. The database's foreign key would refuse an unknown role only when the
/// change is saved, as an exception rather than an answer the caller can act on.
/// </summary>
internal static class TeamMemberRoleIds
{
    /// <summary>Fails naming the first of <paramref name="roleIds"/> that is not a team member role.</summary>
    public static async Task<Result> CheckExist(IOrganizationDbContext organizationDbContext, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken)
    {
        var requested = roleIds.Distinct().ToList();
        var found = await organizationDbContext.TeamMemberRoles
            .Where(r => requested.Contains(r.Id))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        var missing = requested.Except(found).ToList();

        return missing.Count == 0
            ? Result.Success()
            : Result.Failure($"Team member role {missing[0]} not found.");
    }
}
