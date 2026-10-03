using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Authorization;

namespace Wayd.Work.Application.Iterations.Sprints;

/// <summary>
/// Who may start, complete or reopen a team's sprints: a holder of the Update permission who is a member of
/// the team or of the team of teams it belongs to. The Administer permission waives the membership.
/// </summary>
/// <remarks>
/// The controller checks the Update permission too; it is checked again here because the sprint DTO answers
/// the same question for the UI, which must agree with what the commands accept.
/// </remarks>
public static class SprintAuthorization
{
    public static readonly string UpdatePermission =
        ApplicationPermission.NameFor(ApplicationAction.Update, ApplicationResource.Iterations);

    public static readonly string AdministratorPermission =
        ApplicationPermission.NameFor(ApplicationAction.Administer, ApplicationResource.Iterations);

    public const string NotAMemberError = "Only members of the sprint's team, or of its team of teams, can change its lifecycle.";

    public static async Task<bool> CanManageTeamSprints(
        this ICurrentPrincipal currentPrincipal,
        IDispatcher dispatcher,
        Guid teamId,
        LocalDate asOf,
        CancellationToken cancellationToken)
    {
        if (!await currentPrincipal.HasPermission(UpdatePermission, cancellationToken))
            return false;

        if (await currentPrincipal.HasPermission(AdministratorPermission, cancellationToken))
            return true;

        var employeeId = await currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
            return false;

        return await dispatcher.Send(new IsTeamMemberQuery(teamId, employeeId.Value, asOf), cancellationToken);
    }
}
