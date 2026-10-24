namespace Wayd.DemandManagement.Domain.Models;

/// <summary>
/// One employee named as a reviewer on one desk.
/// </summary>
/// <remarks>
/// Desk membership is its own list, never derived from Organization's teams or <c>Employee.Department</c>:
/// a desk is a review responsibility, and one person can review on several.
/// </remarks>
public sealed class IntakeDeskReviewer
{
    private IntakeDeskReviewer() { }

    internal IntakeDeskReviewer(Guid deskId, Guid employeeId)
    {
        DeskId = deskId;
        EmployeeId = employeeId;
    }

    /// <summary>The desk the employee reviews on.</summary>
    public Guid DeskId { get; private init; }

    /// <summary>The employee named as a reviewer.</summary>
    public Guid EmployeeId { get; private init; }
}
