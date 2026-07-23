using NodaTime.Extensions;
using Wayd.ProjectPortfolioManagement.Application.Finalization.Dtos;

namespace Wayd.Web.Api.Models.Ppm.Finalization;

/// <summary>
/// A single CSV row for the finalization import, closing one program or portfolio after its contents have
/// been imported. <see cref="Type"/> discriminates between the two (case-insensitive: "Program" /
/// "Portfolio"); a program row must also name its portfolio, since program names are only unique within one.
/// </summary>
public sealed class ImportPpmFinalizationRequest
{
    public string Type { get; set; } = default!;
    public string Name { get; set; } = default!;

    /// <summary>The portfolio the program belongs to. Required for program rows, ignored for portfolio rows.</summary>
    public string? PortfolioName { get; set; }

    /// <summary>Programs: 'Completed' or 'Cancelled'. Portfolios: 'Closed' or 'Archived'.</summary>
    public string Status { get; set; } = default!;

    /// <summary>The portfolio's end date. Required for portfolio rows, ignored for program rows.</summary>
    public DateTime? EndDate { get; set; }

    public FinalizePpmItemDto ToFinalizePpmItemDto()
    {
        var type = Enum.Parse<FinalizePpmItemType>(Type.Trim(), ignoreCase: true);
        var status = Enum.Parse<FinalizePpmItemStatus>(Status.Trim(), ignoreCase: true);

        return new FinalizePpmItemDto(
            type,
            Name,
            string.IsNullOrWhiteSpace(PortfolioName) ? null : PortfolioName,
            status,
            EndDate?.ToLocalDateTime().Date);
    }
}

public sealed class ImportPpmFinalizationRequestValidator : CustomValidator<ImportPpmFinalizationRequest>
{
    public ImportPpmFinalizationRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(i => i.Type)
            .NotEmpty()
            .Must(t => Enum.TryParse<FinalizePpmItemType>(t.Trim(), ignoreCase: true, out _))
                .WithMessage("Type must be either 'Program' or 'Portfolio'.");

        RuleFor(i => i.Name)
            .NotEmpty();

        RuleFor(i => i.Status)
            .NotEmpty()
            .Must(s => Enum.TryParse<FinalizePpmItemStatus>(s.Trim(), ignoreCase: true, out _))
                .WithMessage("Status must be one of 'Completed', 'Cancelled', 'Closed' or 'Archived'.");
    }
}
