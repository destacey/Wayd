using CsvHelper;
using Wayd.Common.Extensions;
using Wayd.Common.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.Web.Api.Models.Ppm.StrategicInitiatives;

/// <summary>
/// A single CSV row for the strategic initiative import. The owning portfolio is referenced by id and the
/// delivering projects by a semicolon-separated list of their keys; role columns hold semicolon-separated
/// employee numbers. <c>Status</c> is the status the initiative should end up in (case-insensitive),
/// reached by replaying the real lifecycle transitions.
/// </summary>
[CsvRecord]
public sealed partial class ImportStrategicInitiativeRequest
{
    /// <summary>
    /// The caller's own key for this row, unique within the file (case-insensitively). Results are
    /// reported against it, with the id of the initiative it created. Leave a cell blank and the row is
    /// known by its position instead, but the column itself must be in the file.
    /// </summary>
    public string? ImportId { get; set; }

    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;

    /// <summary>The owning portfolio, by id.</summary>
    public Guid PortfolioId { get; set; }

    /// <summary>The initiative's status.</summary>
    [CsvValues(typeof(StrategicInitiativeStatus))]
    public string Status { get; set; } = nameof(StrategicInitiativeStatus.Active);

    /// <summary>The initiative's start date.</summary>
    public DateOnly Start { get; set; }

    /// <summary>The initiative's end date. On or after Start.</summary>
    public DateOnly End { get; set; }

    /// <summary>Semicolon-separated project keys the initiative delivers through.</summary>
    public string? ProjectKeys { get; set; }

    /// <summary>Semicolon-separated employee numbers.</summary>
    public string? Sponsors { get; set; }

    /// <summary>Semicolon-separated employee numbers.</summary>
    public string? Owners { get; set; }

    /// <summary>
    /// Links to add to the strategic initiative, separated by semicolons, each written Name|URL — for example
    /// <c>Charter|https://example.com/charter; Team wiki|https://example.com/wiki</c>. A name cannot contain | or ;,
    /// and a ; inside a URL is written %3B. Each URL must be a full http or https address. A malformed entry rejects
    /// the row.
    /// </summary>
    public string? Links { get; set; }

    public ImportStrategicInitiativeDto ToImportStrategicInitiativeDto()
    {
        var status = Enum.Parse<StrategicInitiativeStatus>(Status.Trim(), ignoreCase: true);

        return new ImportStrategicInitiativeDto(
            Name,
            Description,
            status,
            PortfolioId,
            Start.ToLocalDate(),
            End.ToLocalDate(),
            CsvList.Split(ProjectKeys),
            CsvList.Split(Sponsors),
            CsvList.Split(Owners),
            Links);
    }
}

public sealed class ImportStrategicInitiativeRequestValidator : CustomValidator<ImportStrategicInitiativeRequest>
{
    public ImportStrategicInitiativeRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(i => i.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(i => i.Description)
            .NotEmpty()
            .MaximumLength(2048);

        RuleFor(i => i.PortfolioId)
            .NotEmpty();

        RuleFor(i => i.Status)
            .NotEmpty()
            .Must(s => Enum.TryParse<StrategicInitiativeStatus>(s.Trim(), ignoreCase: true, out _))
                .WithMessage("Status must be one of 'Proposed', 'Approved', 'Active', 'Completed' or 'Canceled'.");

        RuleFor(i => i.Start)
            .NotEmpty();

        RuleFor(i => i.End)
            .NotEmpty()
            .GreaterThanOrEqualTo(i => i.Start)
                .WithMessage("End date must be on or after the start date.");
    }
}
