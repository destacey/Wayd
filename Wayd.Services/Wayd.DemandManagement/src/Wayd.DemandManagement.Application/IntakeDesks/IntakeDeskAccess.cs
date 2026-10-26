namespace Wayd.DemandManagement.Application.IntakeDesks;

/// <summary>
/// The failures desk commands share, which endpoints map to their status codes.
/// </summary>
public static class IntakeDeskAccess
{
    /// <summary>The failure for a desk that does not exist, which an endpoint reports as 404.</summary>
    public const string NotFoundError = "Intake desk not found.";

    public const string NotADeskAdminError = "Only the desk's admins can change it.";

    public const string VisibilityNeedsAdministerError = "Only the Demand Management Admin can change a desk's visibility.";

    public const string DeskAdminsNeedAdministerError = "Only the Demand Management Admin can name, remove or change Desk Admins.";
}
