using Wayd.Planning.Application.PokerSessions.Dtos;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.PokerSessions.Interfaces;

public static class PokerSessionNotifierExtensions
{
    /// <summary>
    /// Tells the session a member joined, left or changed mode, and which unrevealed votes that withdrew.
    /// </summary>
    /// <returns>The member as sent.</returns>
    public static async Task<PokerSessionMemberDto> NotifyMembershipChanged(
        this IPokerSessionNotifier notifier,
        IPlanningDbContext planningDbContext,
        Guid sessionId,
        PokerSessionActor actor,
        PokerSessionMembershipChange change,
        CancellationToken cancellationToken)
    {
        foreach (var roundId in change.WithdrawnRoundIds)
            await notifier.NotifyVoteWithdrawn(sessionId, roundId, actor.UserId);

        var member = await planningDbContext.MemberDto(change.Member, cancellationToken);
        await notifier.NotifyMemberUpdated(sessionId, member);

        return member;
    }
}
