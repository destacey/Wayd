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

    /// <summary>The private session the employee is invited to.</summary>
    public Guid PokerSessionId { get; private init; }

    /// <summary>The invited employee.</summary>
    public Guid EmployeeId { get; private init; }

    /// <summary>The invited employee, when loaded.</summary>
    public Employee? Employee { get; private init; }
}
