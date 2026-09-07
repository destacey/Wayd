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
/// Unlike a portfolio, a program receives its date range on creation; the <see cref="Status"/> transitions
/// only move the status and read that range, so no additional dates are needed on the row.
/// </para>
/// </remarks>
public sealed record ImportProgramDto(
    string Name,
    string Description,
    ProgramStatus Status,
    Guid PortfolioId,
    LocalDate? Start,
    LocalDate? End,
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
        // past Proposed. Canceled is exempt: the domain allows Proposed -> Canceled without dates.
        RuleFor(p => p.Start)
            .NotNull()
            .When(p => p.Status is ProgramStatus.Active or ProgramStatus.Completed)
                .WithMessage("An active or completed program must have a Start and End date.");
    }
}
