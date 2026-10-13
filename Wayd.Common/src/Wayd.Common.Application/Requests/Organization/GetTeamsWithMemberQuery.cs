using NodaTime;

namespace Wayd.Common.Application.Requests.Organization;

/// <summary>
/// <see cref="IsTeamMemberQuery"/> for several teams in one read: the teams among <paramref name="TeamIds"/>
/// that the employee is a member of, or whose team of teams on <paramref name="AsOf"/> they are a member of.
/// </summary>
public sealed record GetTeamsWithMemberQuery(IReadOnlyCollection<Guid> TeamIds, Guid EmployeeId, LocalDate AsOf) : IQuery<IReadOnlySet<Guid>>;
