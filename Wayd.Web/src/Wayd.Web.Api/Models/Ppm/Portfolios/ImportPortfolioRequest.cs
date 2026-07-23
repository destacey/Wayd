using NodaTime.Extensions;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.Web.Api.Models.Ppm.Portfolios;

/// <summary>
/// A single CSV row for the portfolio import. <see cref="Status"/> is the status the portfolio should end
/// up in (case-insensitive), reached by replaying the real lifecycle transitions — which is why
/// <see cref="Start"/> and <see cref="End"/> are on the row: a portfolio only ever gets its date range from
/// those transitions, never from creation.
/// Role columns hold semicolon-separated employee numbers, since a CSV cell cannot carry a list.
/// </summary>
public sealed class ImportPortfolioRequest
{
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;

    /// <summary>The portfolio's status. Defaults to Active when the column is absent.</summary>
    public string Status { get; set; } = nameof(ProjectPortfolioStatus.Active);

    /// <summary>The date the portfolio was activated. Required unless the portfolio is Proposed.</summary>
    public DateTime? Start { get; set; }

    /// <summary>The date the portfolio was closed. Required when Closed or Archived.</summary>
    public DateTime? End { get; set; }

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
            Start?.ToLocalDateTime().Date,
            End?.ToLocalDateTime().Date,
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

        RuleFor(p => p.End)
            .Must((p, end) => end is null || p.Start is null || p.Start <= end)
                .WithMessage("End date must be on or after the start date.");
    }
}
