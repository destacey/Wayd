using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.DemandManagement.Domain.Models;

/// <summary>
/// One employee named on one desk, with what they may do on it.
/// </summary>
/// <remarks>
/// Desk membership is its own list, never derived from Organization's teams or <c>Employee.Department</c>:
/// a desk is a review responsibility, and one person can work on several. Membership is the grant — no
/// claim is needed beside it.
/// </remarks>
public sealed class IntakeDeskMember
{
    private IntakeDeskMember() { }

    internal IntakeDeskMember(Guid deskId, Guid employeeId, IntakeDeskRole role)
    {
        DeskId = deskId;
        EmployeeId = employeeId;
        Role = role;
    }

    /// <summary>The desk the employee is named on.</summary>
    public Guid DeskId { get; private init; }

    /// <summary>The employee named on the desk.</summary>
    public Guid EmployeeId { get; private init; }

    /// <summary>What the employee may do on the desk.</summary>
    public IntakeDeskRole Role { get; private set; }

    /// <summary>Gives the member a different role. Only the desk changes it, which records the change.</summary>
    internal void ChangeRole(IntakeDeskRole role) => Role = role;
}
