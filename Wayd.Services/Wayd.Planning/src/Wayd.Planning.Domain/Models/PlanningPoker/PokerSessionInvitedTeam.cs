namespace Wayd.Planning.Domain.Models.PlanningPoker;

/// <summary>
/// A team invited to a private poker session. The team is stored, not its members, so access follows the team's
/// current membership: its members and the members of its current team of teams.
/// </summary>
public sealed class PokerSessionInvitedTeam
{
    private PokerSessionInvitedTeam() { }

    internal PokerSessionInvitedTeam(Guid pokerSessionId, Guid teamId)
    {
        PokerSessionId = pokerSessionId;
        TeamId = teamId;
    }

    public Guid PokerSessionId { get; private init; }

    public Guid TeamId { get; private init; }

    public PlanningTeam? Team { get; private init; }
}
