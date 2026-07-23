using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.Programs.Dtos;

/// <summary>
/// A single program row. Everything is referenced by natural key so a batch can be authored without knowing
/// generated Ids: the owning portfolio and any strategic themes by name, people by employee number, and
/// projects point back at their program by <see cref="Name"/> in turn.
/// <para>
/// Unlike a portfolio, a program receives its date range on creation; the <see cref="Status"/> transitions
/// only move the status and read that range, so no additional dates are needed on the row.
/// </para>
/// </summary>
public sealed record ImportProgramDto(
    string Name,
    string Description,
    ProgramStatus Status,
    string PortfolioName,
    LocalDate? Start,
    LocalDate? End,
    IReadOnlyList<string> StrategicThemeNames,
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

        RuleFor(p => p.PortfolioName)
            .NotEmpty();

        RuleFor(p => p)
            .Must(p => (p.Start is null && p.End is null) || (p.Start is not null && p.End is not null))
                .WithMessage("Start and End must either both be empty or both have a value.");

        RuleFor(p => p.End)
            .Must((p, end) => end is null || p.Start is null || p.Start <= end)
                .WithMessage("End date must be on or after the start date.");

        // Activating and completing both require a date range, so the row must carry one for any status
        // past Proposed. Cancelled is exempt: the domain allows Proposed -> Cancelled without dates.
        RuleFor(p => p.Start)
            .NotNull()
            .When(p => p.Status is ProgramStatus.Active or ProgramStatus.Completed)
                .WithMessage("An active or completed program must have a Start and End date.");
    }
}
