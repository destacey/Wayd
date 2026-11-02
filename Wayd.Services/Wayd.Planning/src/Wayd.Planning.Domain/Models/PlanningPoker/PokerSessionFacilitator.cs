using Wayd.Common.Domain.Employees;

namespace Wayd.Planning.Domain.Models.PlanningPoker;

/// <summary>
/// An employee who runs a poker session: its rounds, reveals, resets, consensus, details, access and completion.
/// </summary>
public sealed class PokerSessionFacilitator
{
    private PokerSessionFacilitator() { }

    internal PokerSessionFacilitator(Guid pokerSessionId, Guid employeeId)
    {
        PokerSessionId = pokerSessionId;
        EmployeeId = employeeId;
    }

    public Guid PokerSessionId { get; private init; }

    public Guid EmployeeId { get; private init; }

    public Employee? Employee { get; private init; }
}
