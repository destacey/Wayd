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

    /// <summary>
    /// Which of <paramref name="employeeIds"/> the caller may record time away for, as
    /// <see cref="CanManageTimeAway"/> answers for each, asking who the caller is only once.
    /// </summary>
    public static async Task<IReadOnlySet<Guid>> EmployeesWithManageableTimeAway(
        this ICurrentPrincipal currentPrincipal,
        IOrganizationDbContext organizationDbContext,
        IReadOnlyCollection<Guid> employeeIds,
        CancellationToken cancellationToken)
    {
        if (employeeIds.Count == 0)
            return new HashSet<Guid>();

        if (await currentPrincipal.HasPermission(AdministratorPermission, cancellationToken))
            return employeeIds.ToHashSet();

        if (await currentPrincipal.GetEmployeeId(cancellationToken) is not { } callerId)
            return new HashSet<Guid>();

        HashSet<Guid> manageable = [.. employeeIds.Where(id => id == callerId)];
        if (!await organizationDbContext.Employees.AnyAsync(e => e.ManagerId == callerId, cancellationToken))
            return manageable;

        foreach (var employeeId in employeeIds.Where(id => id != callerId))
        {
            if (await ManagerChain.IsAbove(callerId, employeeId, async id =>
                    await organizationDbContext.Employees
                        .Where(e => e.Id == id)
                        .Select(e => e.ManagerId)
                        .FirstOrDefaultAsync(cancellationToken)))
                manageable.Add(employeeId);
        }

        return manageable;
    }

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
