using Wayd.Common.Domain.Employees;
using NodaTime;

namespace Wayd.Planning.Domain.Models.PlanningPoker;

/// <summary>
/// Someone who joined a poker session: the record of who took part, which presence cannot give because it knows
/// only who is connected now. Every member takes part; voting is their own choice, and facilitating is decided by
/// the session's facilitators. There is no leaving: a member who no longer wants to vote turns voting off.
/// </summary>
public sealed class PokerSessionMember
{
    private PokerSessionMember() { }

    internal PokerSessionMember(Guid pokerSessionId, Guid employeeId, bool isVoting, Instant joinedOn)
    {
        PokerSessionId = pokerSessionId;
        EmployeeId = employeeId;
        IsVoting = isVoting;
        JoinedOn = joinedOn;
    }

    /// <summary>The session the employee joined.</summary>
    public Guid PokerSessionId { get; private init; }

    /// <summary>The employee who joined.</summary>
    public Guid EmployeeId { get; private init; }

    /// <summary>The employee who joined, when loaded.</summary>
    public Employee? Employee { get; private init; }

    /// <summary>
    /// Whether the member votes. A voting member counts toward a round's voted and waiting totals; one who is not
    /// voting is left out of them but still takes part in the discussion.
    /// </summary>
    public bool IsVoting { get; private set; }

    /// <summary>
    /// When the member first joined. Joining again keeps it.
    /// </summary>
    public Instant JoinedOn { get; private init; }

    internal void SetVoting(bool isVoting) => IsVoting = isVoting;
}
