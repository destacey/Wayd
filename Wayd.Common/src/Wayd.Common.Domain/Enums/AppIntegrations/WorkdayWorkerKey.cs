namespace Wayd.Common.Domain.Enums.AppIntegrations;

/// <summary>
/// Which Workday worker identifier a Workday connection uses as the upsert key (and therefore as
/// <c>Employee.EmployeeNumber</c> / the value matched during manager resolution).
/// </summary>
public enum WorkdayWorkerKey
{
    /// <summary>The immutable Workday Worker WID. Stable across re-hires and renames.</summary>
    Wid = 0,

    /// <summary>The human-readable Workday <c>Employee_ID</c>. Friendlier; occasionally reassigned by customers.</summary>
    EmployeeId = 1
}
