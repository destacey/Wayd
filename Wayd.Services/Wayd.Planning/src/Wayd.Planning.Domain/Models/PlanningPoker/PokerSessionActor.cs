namespace Wayd.Planning.Domain.Models.PlanningPoker;

/// <summary>
/// The person acting on a poker session, with what the session needs to decide what they may do.
/// </summary>
/// <param name="EmployeeId">The employee the caller acts as. Facilitators, invitees and members are employees.</param>
/// <param name="UserId">The caller's user account, which votes are recorded against.</param>
/// <param name="IsAdministrator">Holds the poker Administer permission, which lets them manage any session.</param>
/// <param name="TeamIds">
/// The session's invited teams the employee belongs to, as <c>IsTeamMemberQuery</c> decides membership. Teams the
/// session does not invite may be left out, since only invited teams grant access.
/// </param>
/// <param name="ManagesHost">
/// Whether the employee manages the record hosting the session, such as an agent on its intake desk, which makes
/// them one of its facilitators. False for a session with no host.
/// </param>
public sealed record PokerSessionActor(Guid EmployeeId, string UserId, bool IsAdministrator, IReadOnlySet<Guid> TeamIds,
    bool ManagesHost = false);
