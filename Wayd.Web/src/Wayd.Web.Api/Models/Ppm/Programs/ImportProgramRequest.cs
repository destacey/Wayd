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

    public DateTime? Start { get; set; }
    public DateTime? End { get; set; }

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
    }
}
