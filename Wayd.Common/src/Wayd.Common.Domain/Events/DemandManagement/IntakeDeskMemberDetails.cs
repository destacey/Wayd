using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// One member of an intake desk and their role on it, as an event records it.
/// </summary>
/// <param name="EmployeeId">The member.</param>
/// <param name="Role">What they may do on the desk.</param>
public sealed record IntakeDeskMemberDetails(Guid EmployeeId, IntakeDeskRole Role);
