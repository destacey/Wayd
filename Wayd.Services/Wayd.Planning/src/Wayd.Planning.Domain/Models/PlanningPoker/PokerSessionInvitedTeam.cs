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

    /// <summary>The private session the team is invited to.</summary>
    public Guid PokerSessionId { get; private init; }

    /// <summary>The invited team or team of teams.</summary>
    public Guid TeamId { get; private init; }

    /// <summary>The invited team, when loaded.</summary>
    public PlanningTeam? Team { get; private init; }
}
