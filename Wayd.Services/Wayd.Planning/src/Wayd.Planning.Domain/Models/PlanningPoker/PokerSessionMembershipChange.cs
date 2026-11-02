namespace Wayd.Planning.Domain.Models.PlanningPoker;

/// <summary>
/// The outcome of joining, leaving or changing mode in a poker session.
/// </summary>
/// <param name="Member">The member as they now stand.</param>
/// <param name="WithdrawnRoundIds">The unrevealed rounds the member's vote was withdrawn from, which others in the session need to hear about.</param>
public sealed record PokerSessionMembershipChange(PokerSessionMember Member, IReadOnlyList<Guid> WithdrawnRoundIds);
