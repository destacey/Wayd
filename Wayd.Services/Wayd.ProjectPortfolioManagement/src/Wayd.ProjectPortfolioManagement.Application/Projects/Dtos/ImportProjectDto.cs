using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;

/// <summary>
/// A single project row.
/// </summary>
/// <remarks>
/// The project's own <see cref="Key"/> is a true natural key (unique in the database) and is what project
/// tasks and strategic initiatives reference. What the project points at is referenced by id — portfolio,
/// program, expenditure category, lifecycle and strategic themes all have names that are not uniquely
/// indexed, so a name is a display value that may match more than one record. People keep their employee
/// number, which is the natural key an employee actually has.
/// <para>
/// <see cref="Start"/> and <see cref="End"/> are the timeline the project plans to run over.
/// <see cref="CreatedOn"/>, <see cref="ActivatedOn"/> and <see cref="ClosedOn"/> are separate from it:
/// they say when the project actually moved, and are what the replayed transitions are stamped with. A
/// project that ran late closed after the end it planned for, so the two are not interchangeable.
/// </para>
/// </remarks>
public sealed record ImportProjectDto(
    string Name,
    string Description,
    ProjectKey Key,
    ProjectStatus Status,
    Guid PortfolioId,
    Guid? ProgramId,
    int ExpenditureCategoryId,
    Guid? ProjectLifecycleId,
    string? BusinessCase,
    string? ExpectedBenefits,
    LocalDate? Start,
    LocalDate? End,
    LocalDate CreatedOn,
    LocalDate? ActivatedOn,
    LocalDate? ClosedOn,
    IReadOnlyList<Guid> StrategicThemeIds,
    IReadOnlyList<string> SponsorEmployeeNumbers,
    IReadOnlyList<string> OwnerEmployeeNumbers,
    IReadOnlyList<string> ManagerEmployeeNumbers,
    IReadOnlyList<string> MemberEmployeeNumbers);

public sealed class ImportProjectDtoValidator : CustomValidator<ImportProjectDto>
{
    public ImportProjectDtoValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(p => p.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(p => p.Description)
            .NotEmpty()
            .MaximumLength(4096);

        RuleFor(p => p.Key)
            .NotNull();

        RuleFor(p => p.Status)
            .IsInEnum();

        RuleFor(p => p.PortfolioId)
            .NotEmpty();

        RuleFor(p => p.ExpenditureCategoryId)
            .GreaterThan(0);

        RuleFor(p => p.BusinessCase)
            .MaximumLength(4096);

        RuleFor(p => p.ExpectedBenefits)
            .MaximumLength(4096);

        RuleFor(p => p)
            .Must(p => (p.Start is null && p.End is null) || (p.Start is not null && p.End is not null))
                .WithMessage("Start and End must either both be empty or both have a value.");

        RuleFor(p => p.End)
            .Must((p, end) => end is null || p.Start is null || p.Start <= end)
                .WithMessage("End date must be on or after the start date.");

        // Activating and completing both require a date range, and so does a canceled row that names an
        // activation, since reaching Canceled through Active still activates. Only a cancellation straight
        // from Proposed is exempt, which the domain allows without dates. Approval requires a lifecycle.
        RuleFor(p => p.Start)
            .NotNull()
            .When(p => p.Status is ProjectStatus.Active or ProjectStatus.Completed || p.ActivatedOn is not null)
                .WithMessage("A project that was activated must have a Start and End date.");

        RuleFor(p => p.ProjectLifecycleId)
            .NotNull()
            .When(p => p.Status is ProjectStatus.Approved)
                .WithMessage("An approved project must have a project lifecycle.");

        // A date for a state the project never reached is rejected rather than dropped: silently ignoring
        // it would import a project whose history disagrees with the file that produced it.
        When(p => p.Status is ProjectStatus.Active or ProjectStatus.Completed,
            () => RuleFor(p => p.ActivatedOn)
                .NotNull()
                    .WithMessage("An active or completed project must have an ActivatedOn date."))
            .Otherwise(() => RuleFor(p => p.ActivatedOn)
                // Canceled is the exception: a project can be canceled either before or after it was
                // activated, and the status alone does not say which.
                .Empty()
                .When(p => p.Status is not ProjectStatus.Canceled)
                    .WithMessage("ActivatedOn is only allowed on a project that reached Active."));

        When(p => p.Status is ProjectStatus.Completed or ProjectStatus.Canceled,
            () => RuleFor(p => p.ClosedOn)
                .NotNull()
                    .WithMessage("A completed or canceled project must have a ClosedOn date."))
            .Otherwise(() => RuleFor(p => p.ClosedOn)
                .Empty()
                    .WithMessage("ClosedOn is only allowed on a project that was completed or canceled."));

        RuleFor(p => p.ActivatedOn)
            .Must((p, activatedOn) => activatedOn is null || p.CreatedOn <= activatedOn)
                .WithMessage("ActivatedOn cannot be earlier than CreatedOn.");

        RuleFor(p => p.ClosedOn)
            .Must((p, closedOn) => closedOn is null || (p.ActivatedOn ?? p.CreatedOn) <= closedOn)
                .WithMessage("ClosedOn cannot be earlier than ActivatedOn or CreatedOn.");
    }
}
