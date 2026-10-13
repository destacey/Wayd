using NodaTime;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Authorization;

namespace Wayd.Organization.Application.EmployeeCapacities;

/// <summary>
/// Who may change how much of a person's time a team has: a holder of the Teams Update permission who is a
/// member of that team or of the team of teams it belongs to. The Teams Administer permission waives the
/// membership.
/// </summary>
/// <remarks>
/// The controller checks the Update permission too; it is checked again here because the allocation DTOs
/// answer the same question for the UI, which must agree with what the commands accept.
/// </remarks>
public static class TeamAllocationAuthorization
{
    public static readonly string UpdatePermission =
        ApplicationPermission.NameFor(ApplicationAction.Update, ApplicationResource.Teams);

    public static readonly string AdministratorPermission =
        ApplicationPermission.NameFor(ApplicationAction.Administer, ApplicationResource.Teams);

    public const string NotAMemberError = "Only members of a team, or of its team of teams, can change the team's allocations.";

    /// <summary>The teams among <paramref name="teamIds"/> whose allocations the caller may change.</summary>
    public static async Task<IReadOnlySet<Guid>> TeamsWithManageableAllocations(
        this ICurrentPrincipal currentPrincipal,
        IDispatcher dispatcher,
        IReadOnlyCollection<Guid> teamIds,
        LocalDate asOf,
        CancellationToken cancellationToken)
    {
        HashSet<Guid> manageable = [];

        if (teamIds.Count == 0 || !await currentPrincipal.HasPermission(UpdatePermission, cancellationToken))
            return manageable;

        if (await currentPrincipal.HasPermission(AdministratorPermission, cancellationToken))
            return teamIds.ToHashSet();

        var employeeId = await currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
            return manageable;

        foreach (var teamId in teamIds.Distinct())
        {
            if (await dispatcher.Send(new IsTeamMemberQuery(teamId, employeeId.Value, asOf), cancellationToken))
                manageable.Add(teamId);
        }

        return manageable;
    }
}
