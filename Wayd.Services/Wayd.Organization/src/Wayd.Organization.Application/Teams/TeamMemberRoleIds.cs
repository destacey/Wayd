namespace Wayd.Organization.Application.Teams;

/// <summary>
/// Checks the roles a team member is given. The database's foreign key would refuse an unknown role only when the
/// change is saved, as an exception rather than an answer the caller can act on.
/// </summary>
internal static class TeamMemberRoleIds
{
    /// <summary>
    /// Fails naming the first of <paramref name="roleIds"/> that is not a team member role, or that is inactive and
    /// not among <paramref name="heldRoleIds"/>. A role the member already holds stays valid after it is deactivated,
    /// so an update that leaves it in place is not refused.
    /// </summary>
    public static async Task<Result> CheckAssignable(
        IOrganizationDbContext organizationDbContext,
        IReadOnlyCollection<Guid> roleIds,
        IReadOnlySet<Guid> heldRoleIds,
        CancellationToken cancellationToken)
    {
        var requested = roleIds.Distinct().ToList();
        var roles = await organizationDbContext.TeamMemberRoles
            .Where(r => requested.Contains(r.Id))
            .Select(r => new { r.Id, r.Name, r.IsActive })
            .ToListAsync(cancellationToken);

        foreach (var roleId in requested)
        {
            var role = roles.Find(r => r.Id == roleId);
            if (role is null)
                return Result.Failure($"Team member role {roleId} not found.");

            if (!role.IsActive && !heldRoleIds.Contains(roleId))
                return Result.Failure($"Inactive roles cannot be assigned to team members. {role.Name} is inactive.");
        }

        return Result.Success();
    }
}
