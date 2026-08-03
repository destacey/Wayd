namespace Wayd.Common.Application.Validators;

public sealed class IExternalEmployeeValidator : CustomValidator<IExternalEmployee>
{
    public IExternalEmployeeValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(e => e.Name)
            .NotNull()
            .SetValidator(new PersonNameValidator());

        RuleFor(e => e.EmployeeNumber)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(e => e.Email)
            .NotNull()
            .SetValidator(new EmailAddressValidator());

        RuleFor(e => e.JobTitle)
            .MaximumLength(256);

        RuleFor(e => e.Department)
            .MaximumLength(256);

        RuleFor(e => e.OfficeLocation)
            .MaximumLength(256);

        // Matches the Employee.EmployeeType column (128). This value is free-form text taken
        // verbatim from the source (Workday's Worker_Type_Reference descriptor, Entra's
        // User.employeeType), so customers configure their own values and an over-long one is
        // plausible. Without this rule it reaches SaveChanges and throws a truncation error that
        // fails the whole batch — every other employee in the payload included.
        RuleFor(e => e.EmployeeType)
            .MaximumLength(128);
    }
}
