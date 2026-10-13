using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// One stretch of an employee's time given to a team, as <see cref="EmployeeAllocationsChangedEvent"/> records
/// the allocations a change added, removed and left in place.
/// </summary>
/// <param name="TeamId">The team or team of teams the time is given to.</param>
/// <param name="RoleId">The team member role the time is spent in, or null when none was chosen.</param>
/// <param name="Start">The first day of the allocation.</param>
/// <param name="End">The last day of the allocation, or null when it has no end.</param>
/// <param name="Percent">The share of the employee's time, from 1 to 100.</param>
public sealed record EmployeeAllocationDetails(Guid TeamId, Guid? RoleId, LocalDate Start, LocalDate? End, int Percent);
