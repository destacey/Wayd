using Wayd.Common.Application.Employees.Commands;

namespace Wayd.Web.Api.Models.Organizations.Employees;

public sealed record CreateEmployeeRequest
{
    /// <summary>Gets the first name.</summary>
    public string FirstName { get; set; } = null!;

    /// <summary>Gets the middle name.</summary>
    public string? MiddleName { get; set; }

    /// <summary>Gets the last name.</summary>
    public string LastName { get; set; } = null!;

    /// <summary>Gets the suffix.</summary>
    public string? Suffix { get; set; }

    /// <summary>Gets the employee's personal title.</summary>
    public string? Title { get; set; }

    /// <summary>Gets the employee number.</summary>
    public string EmployeeNumber { get; set; } = null!;

    /// <summary>Gets the hire date.</summary>
    public Instant? HireDate { get; set; }

    /// <summary>Gets the email.</summary>
    public string Email { get; set; } = null!;

    /// <summary>Gets the job title.</summary>
    public string? JobTitle { get; set; }

    /// <summary>Gets the department.</summary>
    public string? Department { get; set; }

    /// <summary>Gets the office location.</summary>
    public string? OfficeLocation { get; set; }

    /// <summary>Gets the manager identifier.</summary>
    public Guid? ManagerId { get; set; }

    public CreateEmployeeCommand ToCreateEmployeeCommand()
    {
        PersonName personName = new(FirstName, MiddleName, LastName, Suffix, Title);
        EmailAddress emailAddress = (EmailAddress)Email;

        return new CreateEmployeeCommand(personName, EmployeeNumber, HireDate, emailAddress, JobTitle, Department, OfficeLocation, ManagerId);
    }
}

public sealed class CreateEmployeeRequestValidator : CustomValidator<CreateEmployeeRequest>
{
    public CreateEmployeeRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(p => p.FirstName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(p => p.MiddleName)
            .MaximumLength(100);

        RuleFor(p => p.LastName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(p => p.Suffix)
            .MaximumLength(50);

        RuleFor(p => p.Title)
            .MaximumLength(50);

        RuleFor(e => e.EmployeeNumber)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(e => e.Email)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(e => e.JobTitle)
            .MaximumLength(256);

        RuleFor(e => e.Department)
            .MaximumLength(256);

        RuleFor(e => e.OfficeLocation)
            .MaximumLength(256);
    }
}
