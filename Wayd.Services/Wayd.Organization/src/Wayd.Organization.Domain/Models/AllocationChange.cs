using NodaTime;

namespace Wayd.Organization.Domain.Models;

/// <summary>
/// The share of an employee's time a team is to have, in one role, over a date range. It replaces whatever the
/// employee had for that team and role on those days, and leaves the days outside the range as they were.
/// </summary>
/// <param name="TeamId">The team or team of teams.</param>
/// <param name="RoleId">The team member role the time is spent in, or null for none.</param>
/// <param name="Start">The first day the change covers.</param>
/// <param name="End">The last day the change covers, or null for every day from <paramref name="Start"/> on.</param>
/// <param name="Percent">The share of the employee's time, from 0 to 100. Zero ends the allocation for the range.</param>
public sealed record AllocationChange(Guid TeamId, Guid? RoleId, LocalDate Start, LocalDate? End, int Percent);
