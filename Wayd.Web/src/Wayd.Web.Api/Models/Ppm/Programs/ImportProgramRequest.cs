using NodaTime.Extensions;
using Wayd.ProjectPortfolioManagement.Application.Programs.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.Web.Api.Models.Ppm.Programs;

/// <summary>
/// A single CSV row for the program import. The owning portfolio is referenced by id and strategic themes
/// by a semicolon-separated list of ids; role columns hold semicolon-separated employee numbers.
/// <see cref="Status"/> is the status the program should end up in (case-insensitive), reached by replaying
/// the real lifecycle transitions.
/// </summary>
public sealed class ImportProgramRequest
{
    /// <summary>
    /// The caller's own key for this row, unique within the file (case-insensitively). Results are
    /// reported against it. Falls back to the row's position when the column is absent, so a
    /// hand-authored file still works.
    /// </summary>
    public string? ImportId { get; set; }

    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public Guid PortfolioId { get; set; }

    /// <summary>The program's status. Defaults to Active when the column is absent.</summary>
    public string Status { get; set; } = nameof(ProgramStatus.Active);

    /// <summary>The timeline the program plans to run over.</summary>
    public DateTime? Start { get; set; }
    public DateTime? End { get; set; }

    /// <summary>
    /// The date the program was proposed. Required on every row. Nothing stores it yet — a program keeps
    /// no transition dates beyond the audit stamp, which records when the file was uploaded — but the
    /// column is required now so that no file has to change on the day one is kept.
    /// </summary>
    public DateTime? CreatedOn { get; set; }

    /// <summary>
    /// The date the program became active. Required once the status is Active or Completed, optional on a
    /// canceled program, and rejected on one that never got that far. There is no closing date here: an
    /// import cannot complete or cancel a program, so the finalize import carries that one.
    /// </summary>
    public DateTime? ActivatedOn { get; set; }

    /// <summary>Semicolon-separated strategic theme ids.</summary>
    public string? StrategicThemes { get; set; }
    public string? Sponsors { get; set; }
    public string? Owners { get; set; }
    public string? Managers { get; set; }

    public ImportProgramDto ToImportProgramDto()
    {
        var status = Enum.Parse<ProgramStatus>(Status.Trim(), ignoreCase: true);

        return new ImportProgramDto(
            Name,
            Description,
            status,
            PortfolioId,
            Start?.ToLocalDateTime().Date,
            End?.ToLocalDateTime().Date,
            // Required by the validator that runs before this mapping.
            CreatedOn!.Value.ToLocalDateTime().Date,
            ActivatedOn?.ToLocalDateTime().Date,
            CsvList.SplitIds(StrategicThemes),
            CsvList.Split(Sponsors),
            CsvList.Split(Owners),
            CsvList.Split(Managers));
    }
}

public sealed class ImportProgramRequestValidator : CustomValidator<ImportProgramRequest>
{
    public ImportProgramRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(p => p.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(p => p.Description)
            .NotEmpty()
            .MaximumLength(2048);

        RuleFor(p => p.PortfolioId)
            .NotEmpty();

        RuleFor(p => p.StrategicThemes)
            .Must(CsvList.AreAllIds)
                .WithMessage("StrategicThemes must be a semicolon-separated list of ids.");

        RuleFor(p => p.Status)
            .NotEmpty()
            .Must(s => Enum.TryParse<ProgramStatus>(s.Trim(), ignoreCase: true, out _))
                .WithMessage("Status must be one of 'Proposed', 'Active', 'Completed' or 'Canceled'.");

        RuleFor(p => p)
            .Must(p => (p.Start is null && p.End is null) || (p.Start is not null && p.End is not null))
                .WithMessage("Start and End must either both be empty or both have a value.");

        RuleFor(p => p.End)
            .Must((p, end) => end is null || p.Start is null || p.Start <= end)
                .WithMessage("End date must be on or after the start date.");

        RuleFor(p => p.CreatedOn)
            .NotNull()
                .WithMessage("Every program must have a CreatedOn date.");

        // Which of the remaining two a row may carry depends on the status, which is parsed here rather
        // than compared as text so 'active' and 'Active' are the same row.
        When(p => StatusOf(p) is ProgramStatus.Active or ProgramStatus.Completed,
            () => RuleFor(p => p.ActivatedOn)
                .NotNull()
                    .WithMessage("An active or completed program must have an ActivatedOn date."))
            .Otherwise(() => RuleFor(p => p.ActivatedOn)
                .Empty()
                .When(p => StatusOf(p) is not (null or ProgramStatus.Canceled))
                    .WithMessage("ActivatedOn is only allowed on a program that reached Active."));

        RuleFor(p => p.ActivatedOn)
            .Must((p, activatedOn) => activatedOn is null || p.CreatedOn is null || p.CreatedOn <= activatedOn)
                .WithMessage("ActivatedOn cannot be earlier than CreatedOn.");
    }

    /// <summary>
    /// The row's status, or null when it does not name a real one — the status rule reports that, so the
    /// date rules stay quiet rather than piling a second complaint onto the same row.
    /// </summary>
    private static ProgramStatus? StatusOf(ImportProgramRequest request) =>
        Enum.TryParse<ProgramStatus>(request.Status?.Trim(), ignoreCase: true, out var status) ? status : null;
}
