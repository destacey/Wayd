using Wayd.Common.Domain.Employees;
using Wayd.Planning.Domain.Enums;
using NodaTime;

namespace Wayd.Planning.Domain.Models.PlanningPoker;

/// <summary>
/// Someone who joined a poker session: the record of who took part, which presence cannot give because it knows
/// only who is connected now. Leaving keeps the row and stamps <see cref="LeftOn"/>.
/// </summary>
public sealed class PokerSessionMember
{
    private PokerSessionMember() { }

    internal PokerSessionMember(Guid pokerSessionId, Guid employeeId, PokerSessionMemberMode mode, Instant joinedOn)
    {
        PokerSessionId = pokerSessionId;
        EmployeeId = employeeId;
        Mode = mode;
        JoinedOn = joinedOn;
    }

    public Guid PokerSessionId { get; private init; }

    public Guid EmployeeId { get; private init; }

    public Employee? Employee { get; private init; }

    /// <summary>
    /// How the member takes part now. Kept after they leave, as the mode they left in.
    /// </summary>
    public PokerSessionMemberMode Mode { get; private set; }

    /// <summary>
    /// When the member first joined. Rejoining after leaving keeps it.
    /// </summary>
    public Instant JoinedOn { get; private init; }

    /// <summary>
    /// When the member left, or null while they are in the session.
    /// </summary>
    public Instant? LeftOn { get; private set; }

    /// <summary>
    /// True while the member is in the session, that is, until they leave.
    /// </summary>
    public bool IsPresent => LeftOn is null;

    internal void Rejoin(PokerSessionMemberMode mode)
    {
        Mode = mode;
        LeftOn = null;
    }

    internal void ChangeMode(PokerSessionMemberMode mode) => Mode = mode;

    internal void Leave(Instant timestamp) => LeftOn = timestamp;
}
