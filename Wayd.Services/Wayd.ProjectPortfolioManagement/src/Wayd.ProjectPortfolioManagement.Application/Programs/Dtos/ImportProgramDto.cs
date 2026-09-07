using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.Programs.Dtos;

/// <summary>
/// A single program row.
/// </summary>
/// <remarks>
/// The owning portfolio and any strategic themes are referenced by id — neither name is uniquely indexed,
/// so a name is a display value that may match more than one record. People keep their employee number,
/// which is the natural key an employee actually has.
/// <para>
/// Unlike a portfolio, a program receives its date range on creation: <see cref="Start"/> and
/// <see cref="End"/> are the timeline it plans to run over, and the <see cref="Status"/> transitions read
/// that range rather than setting it.
/// </para>
/// <para>
/// <see cref="CreatedOn"/> and <see cref="ActivatedOn"/> are separate from that timeline: they say when
/// the program actually moved. Nothing stores them yet — a program records no transition dates at all,
/// and its audit stamp is when the file was uploaded — but they are required and checked now so that the
/// day it does record them, no file has to change. There is no closing date here: an import cannot
/// complete or cancel a program, since that can only happen once its projects are closed, so the finalize
/// import carries that date on its own row.
/// </para>
/// </remarks>
public sealed record ImportProgramDto(
    string Name,
    string Description,
    ProgramStatus Status,
    Guid PortfolioId,
    LocalDate? Start,
    LocalDate? End,
    LocalDate CreatedOn,
    LocalDate? ActivatedOn,
    IReadOnlyList<Guid> StrategicThemeIds,
    IReadOnlyList<string> SponsorEmployeeNumbers,
    IReadOnlyList<string> OwnerEmployeeNumbers,
    IReadOnlyList<string> ManagerEmployeeNumbers);

public sealed class ImportProgramDtoValidator : CustomValidator<ImportProgramDto>
{
    public ImportProgramDtoValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(p => p.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(p => p.Description)
            .NotEmpty()
            .MaximumLength(2048);

        RuleFor(p => p.Status)
            .IsInEnum();

        RuleFor(p => p.PortfolioId)
            .NotEmpty();

        RuleFor(p => p)
            .Must(p => (p.Start is null && p.End is null) || (p.Start is not null && p.End is not null))
                .WithMessage("Start and End must either both be empty or both have a value.");

        RuleFor(p => p.End)
            .Must((p, end) => end is null || p.Start is null || p.Start <= end)
                .WithMessage("End date must be on or after the start date.");

        // Activating and completing both require a date range, so the row must carry one for any status
        // past Proposed — and for a canceled program that names an activation, since reaching Canceled
        // through Active still activates. Canceled without one is exempt: the domain allows
        // Proposed -> Canceled without dates.
        RuleFor(p => p.Start)
            .NotNull()
            .When(p => p.Status is ProgramStatus.Active or ProgramStatus.Completed || p.ActivatedOn is not null)
                .WithMessage("A program that was activated must have a Start and End date.");

        // A date for a state the program never reached is rejected rather than dropped: silently ignoring
        // it would import a program whose dates disagree with the file that produced it.
        When(p => p.Status is ProgramStatus.Active or ProgramStatus.Completed,
            () => RuleFor(p => p.ActivatedOn)
                .NotNull()
                    .WithMessage("An active or completed program must have an ActivatedOn date."))
            .Otherwise(() => RuleFor(p => p.ActivatedOn)
                // Canceled is the exception: a program can be canceled either before or after it was
                // activated, and the status alone does not say which.
                .Empty()
                .When(p => p.Status is not ProgramStatus.Canceled)
                    .WithMessage("ActivatedOn is only allowed on a program that reached Active."));

        RuleFor(p => p.ActivatedOn)
            .Must((p, activatedOn) => activatedOn is null || p.CreatedOn <= activatedOn)
                .WithMessage("ActivatedOn cannot be earlier than CreatedOn.");
    }
}
