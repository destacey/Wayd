namespace Wayd.Organization.Application.TeamsOfTeams;

/// <summary>
/// Loads the whole subtree below a team of teams, which the domain's cycle check walks when a parent
/// membership is added or its dates change. Without it the check sees no children and passes every time.
/// </summary>
internal static class TeamOfTeamsDescendants
{
    /// <summary>
    /// Loads every child membership below <paramref name="team"/>, past, current and future, with each
    /// membership's child team.
    /// </summary>
    public static Task Load(IOrganizationDbContext dbContext, TeamOfTeams team, CancellationToken cancellationToken)
        => Load(dbContext, team, [], cancellationToken);

    // The path guard stops a cycle already in the data from recursing forever.
    private static async Task Load(IOrganizationDbContext dbContext, TeamOfTeams team, HashSet<Guid> path, CancellationToken cancellationToken)
    {
        if (!path.Add(team.Id))
            return;

        await dbContext.Entry(team)
            .Collection(e => e.ChildMemberships)
            .LoadAsync(cancellationToken);

        foreach (var membership in team.ChildMemberships)
        {
            await dbContext.Entry(membership)
                .Reference(e => e.Source)
                .LoadAsync(cancellationToken);

            if (membership.Source is TeamOfTeams child)
                await Load(dbContext, child, path, cancellationToken);
        }

        path.Remove(team.Id);
    }
}
