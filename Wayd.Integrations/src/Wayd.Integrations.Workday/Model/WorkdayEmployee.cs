using Wayd.Common.Application.Interfaces;
using Wayd.Common.Models;
using NodaTime;

namespace Wayd.Integrations.Workday.Model;

/// <summary>
/// Maps a Workday Staffing <c>Worker</c> response payload into Wayd's <see cref="IExternalEmployee"/>
/// contract. Constructed from primitive fields the SOAP client already extracted — keeping
/// SOAP-generated proxy types out of the public surface.
/// </summary>
public sealed record WorkdayEmployee : IExternalEmployee
{
    public WorkdayEmployee(
        string employeeNumber,
        PersonName name,
        EmailAddress email,
        Instant? hireDate,
        string? jobTitle,
        string? department,
        string? officeLocation,
        string? managerEmployeeNumber,
        bool isActive,
        string? employeeType)
    {
        EmployeeNumber = employeeNumber;
        Name = name;
        Email = email;
        HireDate = hireDate;
        JobTitle = jobTitle;
        Department = department;
        OfficeLocation = officeLocation;
        ManagerEmployeeNumber = managerEmployeeNumber;
        IsActive = isActive;
        EmployeeType = employeeType;
    }

    public string EmployeeNumber { get; }
    public PersonName Name { get; }
    public Instant? HireDate { get; }
    public EmailAddress Email { get; }
    public string? JobTitle { get; }
    public string? Department { get; }
    public string? OfficeLocation { get; }
    public string? ManagerEmployeeNumber { get; }
    public bool IsActive { get; }
    public string? EmployeeType { get; }
}
