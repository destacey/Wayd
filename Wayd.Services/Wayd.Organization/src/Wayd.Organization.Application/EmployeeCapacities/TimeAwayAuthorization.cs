using Wayd.Common.Domain.Authorization;

namespace Wayd.Organization.Application.EmployeeCapacities;

/// <summary>
/// Who may record an employee's time away, and so who is shown why they are away: the employee, anyone they
/// report up to at any depth, and a holder of the Administer Employee Time Away permission.
/// </summary>
/// <remarks>
/// The first two come from who the caller is, and need no permission. The time away read answers the same
/// question for the UI and decides with it whether to show the reason, so it and the commands both ask here.
/// </remarks>
public static class TimeAwayAuthorization
{
    public static readonly string AdministratorPermission =
        ApplicationPermission.NameFor(ApplicationAction.Administer, ApplicationResource.EmployeeTimeAway);

    public const string NotAllowedError = "Only the employee, the people they report to, or a time away administrator can record, change or remove their time away.";

    /// <summary>Whether the caller may record time away for <paramref name="employeeId"/> and see its reason.</summary>
    public static async Task<bool> CanManageTimeAway(
        this ICurrentPrincipal currentPrincipal,
        IOrganizationDbContext organizationDbContext,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        if (await currentPrincipal.HasPermission(AdministratorPermission, cancellationToken))
            return true;

        var callerId = await currentPrincipal.GetEmployeeId(cancellationToken);
        if (callerId is null)
            return false;

        if (callerId == employeeId)
            return true;

        // Most callers manage nobody, and the page reading time away asks this on every load.
        var manager = callerId.Value;
        if (!await organizationDbContext.Employees.AnyAsync(e => e.ManagerId == manager, cancellationToken))
            return false;

        return await ManagerChain.IsAbove(manager, employeeId, async id =>
            await organizationDbContext.Employees
                .Where(e => e.Id == id)
                .Select(e => e.ManagerId)
                .FirstOrDefaultAsync(cancellationToken));
    }
}
