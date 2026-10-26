using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// A member of an intake desk given a different role on it, as an event records it.
/// </summary>
/// <param name="EmployeeId">The member.</param>
/// <param name="FromRole">The role they held.</param>
/// <param name="ToRole">The role they hold now.</param>
public sealed record IntakeDeskRoleChange(Guid EmployeeId, IntakeDeskRole FromRole, IntakeDeskRole ToRole);
