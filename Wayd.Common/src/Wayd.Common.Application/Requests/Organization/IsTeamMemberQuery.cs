using NodaTime;

namespace Wayd.Common.Application.Requests.Organization;

/// <summary>
/// Whether an employee is a member of an Organization team, or of the team of teams it belonged to on
/// <paramref name="AsOf"/>, in any role. Only the direct parent counts, not the parents above it.
/// </summary>
public sealed record IsTeamMemberQuery(Guid TeamId, Guid EmployeeId, LocalDate AsOf) : IQuery<bool>;
