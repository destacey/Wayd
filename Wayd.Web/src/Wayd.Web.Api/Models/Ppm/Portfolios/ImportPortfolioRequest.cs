using NodaTime.Extensions;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.Web.Api.Models.Ppm.Portfolios;

/// <summary>
/// A single CSV row for the portfolio import. <see cref="Status"/> is the status the portfolio should end
/// up in (case-insensitive), reached by replaying the real lifecycle transitions — which is why the
/// transition dates are on the row: a portfolio only ever gets its date range from those transitions,
/// never from creation.
/// Role columns hold semicolon-separated employee numbers, since a CSV cell cannot carry a list.
/// </summary>
public sealed class ImportPortfolioRequest
{
    /// <summary>
    /// The caller's own key for this row, unique within the file (case-insensitively). Results are
    /// reported against it. Falls back to the row's position when the column is absent, so a
    /// hand-authored file still works.
    /// </summary>
    public string? ImportId { get; set; }

    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;

    /// <summary>The portfolio's status. Defaults to Active when the column is absent.</summary>
    public string Status { get; set; } = nameof(ProjectPortfolioStatus.Active);

    /// <summary>
    /// The date the portfolio was proposed. Required on every row. Nothing stores it yet — a portfolio
    /// keeps no creation date beyond the audit stamp, which records when the file was uploaded — but the
    /// column is required now so that no file has to change on the day one is kept.
    /// </summary>
    public DateTime? CreatedOn { get; set; }

    /// <summary>
    /// The date the portfolio was activated. Required unless the portfolio is Proposed. There is no
    /// closing date here: an import cannot close a portfolio, so the finalize import carries that one.
    /// </summary>
    public DateTime? ActivatedOn { get; set; }

    public string? Sponsors { get; set; }
    public string? Owners { get; set; }
    public string? Managers { get; set; }

    public ImportProjectPortfolioDto ToImportProjectPortfolioDto()
    {
        var status = Enum.Parse<ProjectPortfolioStatus>(Status.Trim(), ignoreCase: true);

        return new ImportProjectPortfolioDto(
            Name,
            Description,
            status,
            // Required by the validator that runs before this mapping.
            CreatedOn!.Value.ToLocalDateTime().Date,
            ActivatedOn?.ToLocalDateTime().Date,
            CsvList.Split(Sponsors),
            CsvList.Split(Owners),
            CsvList.Split(Managers));
    }
}

public sealed class ImportPortfolioRequestValidator : CustomValidator<ImportPortfolioRequest>
{
    public ImportPortfolioRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(p => p.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(p => p.Description)
            .NotEmpty()
            .MaximumLength(1024);

        RuleFor(p => p.Status)
            .NotEmpty()
            .Must(s => Enum.TryParse<ProjectPortfolioStatus>(s.Trim(), ignoreCase: true, out _))
                .WithMessage("Status must be one of 'Proposed', 'Active', 'OnHold', 'Closed' or 'Archived'.");

        RuleFor(p => p.CreatedOn)
            .NotNull()
                .WithMessage("Every portfolio must have a CreatedOn date.");

        // Which of the remaining two a row may carry depends on the status, which is parsed here rather
        // than compared as text so 'active' and 'Active' are the same row.
        When(p => StatusOf(p) is not (null or ProjectPortfolioStatus.Proposed),
            () => RuleFor(p => p.ActivatedOn)
                .NotNull()
                    .WithMessage("A portfolio that is not proposed must have an ActivatedOn date."))
            .Otherwise(() => RuleFor(p => p.ActivatedOn)
                .Empty()
                .When(p => StatusOf(p) is not null)
                    .WithMessage("ActivatedOn is only allowed on a portfolio that reached Active."));

        RuleFor(p => p.ActivatedOn)
            .Must((p, activatedOn) => activatedOn is null || p.CreatedOn is null || p.CreatedOn <= activatedOn)
                .WithMessage("ActivatedOn cannot be earlier than CreatedOn.");
    }

    /// <summary>
    /// The row's status, or null when it does not name a real one — the status rule reports that, so the
    /// date rules stay quiet rather than piling a second complaint onto the same row.
    /// </summary>
    private static ProjectPortfolioStatus? StatusOf(ImportPortfolioRequest request) =>
        Enum.TryParse<ProjectPortfolioStatus>(request.Status?.Trim(), ignoreCase: true, out var status) ? status : null;
}
