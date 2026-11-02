using Wayd.Common.Domain.Employees;

namespace Wayd.Planning.Domain.Models.PlanningPoker;

/// <summary>
/// An employee invited to a private poker session. An invite grants access only: it does not decide whether the
/// person joins as a Participant or an Observer, and they still need the poker View permission.
/// </summary>
public sealed class PokerSessionInvitedEmployee
{
    private PokerSessionInvitedEmployee() { }

    internal PokerSessionInvitedEmployee(Guid pokerSessionId, Guid employeeId)
    {
        PokerSessionId = pokerSessionId;
        EmployeeId = employeeId;
    }

    public Guid PokerSessionId { get; private init; }

    public Guid EmployeeId { get; private init; }

    public Employee? Employee { get; private init; }
}
