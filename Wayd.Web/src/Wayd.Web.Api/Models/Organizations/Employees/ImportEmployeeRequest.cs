using Wayd.Common.Application.Employees.Dtos;

namespace Wayd.Web.Api.Models.Organizations.Employees;

/// <summary>
/// A single CSV row for the employee import. References the row's manager by employee number so an entire
/// management tree can be imported at once; linkage is resolved server-side after all rows are created.
/// </summary>
public sealed class ImportEmployeeRequest
{
    public string EmployeeNumber { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public DateTime? HireDate { get; set; }
    public string? JobTitle { get; set; }
    public string? Department { get; set; }
    public string? OfficeLocation { get; set; }
    public string? ManagerNumber { get; set; }

    /// <summary>Whether the employee is currently active. Defaults to true when the column is absent.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>The worker type (e.g. Employee, Contractor, Intern), mirroring the HRIS descriptor. Free-form.</summary>
    public string? EmployeeType { get; set; }

    public ImportEmployeeDto ToImportEmployeeDto()
    {
        Instant? hireDate = HireDate.HasValue
            ? Instant.FromDateTimeUtc(DateTime.SpecifyKind(HireDate.Value, DateTimeKind.Utc))
            : null;

        return new ImportEmployeeDto(
            EmployeeNumber,
            FirstName,
            MiddleName,
            LastName,
            (EmailAddress)Email,
            hireDate,
            JobTitle,
            Department,
            OfficeLocation,
            string.IsNullOrWhiteSpace(ManagerNumber) ? null : ManagerNumber.Trim(),
            IsActive,
            string.IsNullOrWhiteSpace(EmployeeType) ? null : EmployeeType.Trim());
    }
}

public sealed class ImportEmployeeRequestValidator : CustomValidator<ImportEmployeeRequest>
{
    public ImportEmployeeRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(e => e.EmployeeNumber)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(e => e.FirstName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(e => e.MiddleName)
            .MaximumLength(100);

        RuleFor(e => e.LastName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(e => e.Email)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(e => e.JobTitle)
            .MaximumLength(256);

        RuleFor(e => e.Department)
            .MaximumLength(256);

        RuleFor(e => e.OfficeLocation)
            .MaximumLength(256);

        RuleFor(e => e.EmployeeType)
            .MaximumLength(256);
    }
}
