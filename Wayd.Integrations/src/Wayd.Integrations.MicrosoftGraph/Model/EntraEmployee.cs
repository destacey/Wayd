using Ardalis.GuardClauses;
using Microsoft.Graph.Models;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Models;
using NodaTime;

namespace Wayd.Integrations.MicrosoftGraph.Model;

public sealed record EntraEmployee : IExternalEmployee
{
    public EntraEmployee(User user)
    {
        EmployeeNumber = Guard.Against.NullOrWhiteSpace(user.EmployeeId ?? user.Id);
        Name = new PersonName(Guard.Against.NullOrWhiteSpace(user.GivenName), null, Guard.Against.NullOrWhiteSpace(user.Surname));
        HireDate = user.HireDate is not null
            ? Instant.FromDateTimeOffset((DateTimeOffset)user.HireDate)
            : user.EmployeeHireDate is not null
                ? Instant.FromDateTimeOffset((DateTimeOffset)user.EmployeeHireDate)
                : null;
        Email = new Common.Models.EmailAddress(user.Mail ?? Guard.Against.NullOrWhiteSpace(user.UserPrincipalName));
        JobTitle = user.JobTitle;
        Department = user.Department;
        OfficeLocation = user.OfficeLocation;
        ManagerEmployeeNumber = user.Manager?.Id;
        IsActive = user.AccountEnabled ?? false;
        // Pass Microsoft Graph's free-form employeeType through verbatim. Customers configure it
        // in their tenant; we don't normalize.
        EmployeeType = string.IsNullOrWhiteSpace(user.EmployeeType) ? null : user.EmployeeType.Trim();
    }

    public string EmployeeNumber { get; set; }
    public PersonName Name { get; set; }
    public Instant? HireDate { get; set; }
    public Common.Models.EmailAddress Email { get; set; }
    public string? JobTitle { get; set; }
    public string? Department { get; set; }
    public string? OfficeLocation { get; set; }
    public string? ManagerEmployeeNumber { get; set; }
    public bool IsActive { get; set; }
    public string? EmployeeType { get; set; }
}
