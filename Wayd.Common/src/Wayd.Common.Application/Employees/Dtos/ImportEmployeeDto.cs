using Wayd.Common.Application.Validators;
using Wayd.Common.Models;

namespace Wayd.Common.Application.Employees.Dtos;

/// <summary>
/// A single employee row to be imported. References its manager by <see cref="ManagerNumber"/>
/// (the manager's <see cref="EmployeeNumber"/>) rather than by Id, so a whole management tree
/// can be imported in one pass without the caller knowing the generated Ids. Manager linkage is
/// resolved inside the handler after every row has been created.
/// <para>
/// A row may represent a former employee by setting <see cref="IsActive"/> to <c>false</c> (useful for
/// migrations and historical test fixtures). The employee is created active and then deactivated through
/// the domain, so the deactivation event fires.
/// </para>
/// </summary>
/// <param name="AccessEndDate">
/// The last day the employee keeps access. Required: an imported employee is owned by Wayd, so only this date
/// ends its access.
/// </param>
/// <param name="AdditionalEmails">
/// Further work addresses this person is known by, beyond <paramref name="Email"/>. Used to match people
/// against systems still referencing an address they have moved away from.
/// </param>
public sealed record ImportEmployeeDto(
    string EmployeeNumber,
    string FirstName,
    string? MiddleName,
    string LastName,
    EmailAddress Email,
    Instant? HireDate,
    string? JobTitle,
    string? Department,
    string? OfficeLocation,
    string? ManagerNumber,
    bool IsActive = true,
    string? EmployeeType = null,
    IReadOnlyList<EmailAddress>? AdditionalEmails = null,
    LocalDate? AccessEndDate = null);

public sealed class ImportEmployeeDtoValidator : CustomValidator<ImportEmployeeDto>
{
    public ImportEmployeeDtoValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(e => e.EmployeeNumber)
            .NotEmpty()
            .MaximumLength(64);

        // An imported employee is owned by Wayd, so no people sync ends its access.
        RuleFor(e => e.AccessEndDate)
            .NotNull();

        RuleFor(e => e.FirstName)
            .NotEmpty();

        RuleFor(e => e.LastName)
            .NotEmpty();

        RuleFor(e => e.Email)
            .NotNull()
            .SetValidator(new EmailAddressValidator());

        RuleFor(e => e.JobTitle)
            .MaximumLength(256);

        RuleFor(e => e.Department)
            .MaximumLength(256);

        RuleFor(e => e.OfficeLocation)
            .MaximumLength(256);

        RuleFor(e => e.EmployeeType)
            .MaximumLength(128);
    }
}
