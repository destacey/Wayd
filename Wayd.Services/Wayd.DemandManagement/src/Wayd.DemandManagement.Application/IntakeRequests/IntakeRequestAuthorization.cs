using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.DemandManagement.Application.IntakeRequests;

/// <summary>
/// Resolves who is reading or changing intake requests: the two claims Demand Management uses and the desks
/// the user is a member of.
/// </summary>
/// <remarks>
/// Claims say what someone may do anywhere and desk roles what they may do on a desk; no action needs both.
/// Most endpoints therefore need only a signed-in user, and the handler decides from here. The DTOs answer the
/// same questions for the UI, which must agree with what the commands accept.
/// </remarks>
public static class IntakeRequestAuthorization
{
    public static readonly string ViewPermission =
        ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.IntakeRequests);

    public static readonly string AdministerPermission =
        ApplicationPermission.NameFor(ApplicationAction.Administer, ApplicationResource.IntakeRequests);

    public const string NotAnAgentError = "Only agents on the request's desk can do this.";

    public const string NotADeskAdminError = "Only the desk's admins can do this.";

    /// <summary>
    /// The failure a command returns for a request the caller cannot see, which an endpoint reports as 404
    /// exactly as it does a request that does not exist.
    /// </summary>
    public const string NotFoundError = "Intake request not found.";

    /// <summary>
    /// The current user as a reader of intake requests. View and Administer are read whatever the request's
    /// token scope, because they widen what the reader sees; a capability hint built from the reader also checks
    /// <see cref="ICurrentPrincipal.IsReadOnly"/>.
    /// </summary>
    /// <remarks>
    /// An inactive employee is a member of no desk, so leaving the company ends desk access without anyone
    /// editing a desk.
    /// </remarks>
    public static async Task<IntakeRequestReader> ResolveIntakeRequestReader(
        this ICurrentPrincipal currentPrincipal,
        IDemandManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var employeeId = await currentPrincipal.GetEmployeeId(cancellationToken);
        var holdsView = await currentPrincipal.HoldsPermission(ViewPermission, cancellationToken);
        var isDemandManagementAdmin = await currentPrincipal.HoldsPermission(AdministerPermission, cancellationToken);

        Dictionary<Guid, IntakeDeskRole> deskRoles = [];
        if (!isDemandManagementAdmin && employeeId is { } id
            && await dbContext.Employees.AnyAsync(e => e.Id == id && e.IsActive, cancellationToken))
        {
            deskRoles = await dbContext.IntakeDesks
                .SelectMany(d => d.Members)
                .Where(m => m.EmployeeId == id)
                .ToDictionaryAsync(m => m.DeskId, m => m.Role, cancellationToken);
        }

        return new IntakeRequestReader(employeeId, holdsView, isDemandManagementAdmin, deskRoles);
    }
}
