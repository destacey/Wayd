using Wayd.Common.Models;

namespace Wayd.Common.Application.Interfaces;

public interface IExternalEmployee
{
    string EmployeeNumber { get; }
    PersonName Name { get; }
    Instant? HireDate { get; }
    EmailAddress Email { get; }
    string? JobTitle { get; }
    string? Department { get; }
    string? OfficeLocation { get; }
    string? ManagerEmployeeNumber { get; }
    bool IsActive { get; }

    /// <summary>
    /// The source system's classification of this worker's employment type, taken verbatim from
    /// the upstream (Workday Worker_Type_Reference descriptor, Entra <c>User.employeeType</c>).
    /// Free-form because customers configure their own taxonomy.
    /// </summary>
    string? EmployeeType { get; }
}
