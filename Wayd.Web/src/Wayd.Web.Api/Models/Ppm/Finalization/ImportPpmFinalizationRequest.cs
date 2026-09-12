using Wayd.Common.Extensions;
using Wayd.ProjectPortfolioManagement.Application.Finalization.Dtos;

namespace Wayd.Web.Api.Models.Ppm.Finalization;

/// <summary>
/// A single CSV row for the finalization import, closing one program or portfolio after its contents have
/// been imported. <see cref="Type"/> discriminates between the two (case-insensitive: "Program" /
/// "Portfolio") and says how to read <see cref="Id"/>.
/// </summary>
public sealed class ImportPpmFinalizationRequest
{
    /// <summary>
    /// The caller's own key for this row, unique within the file (case-insensitively). Results are
    /// reported against it. Falls back to the row's position when the column is absent, so a
    /// hand-authored file still works.
    /// </summary>
    public string? ImportId { get; set; }

    public string Type { get; set; } = default!;

    /// <summary>The program or portfolio this row closes, per Type.</summary>
    public Guid Id { get; set; }

    /// <summary>Programs: 'Completed' or 'Canceled'. Portfolios: 'Closed' or 'Archived'.</summary>
    public string Status { get; set; } = default!;

    /// <summary>The portfolio's end date. Required for portfolio rows, ignored for program rows.</summary>
    public DateOnly? EndDate { get; set; }

    public FinalizePpmItemDto ToFinalizePpmItemDto()
    {
        var type = Enum.Parse<FinalizePpmItemType>(Type.Trim(), ignoreCase: true);
        var status = Enum.Parse<FinalizePpmItemStatus>(Status.Trim(), ignoreCase: true);

        return new FinalizePpmItemDto(type, Id, status, EndDate?.ToLocalDate());
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

        RuleFor(i => i.Id)
            .NotEmpty();

        RuleFor(i => i.Status)
            .NotEmpty()
            .Must(s => Enum.TryParse<FinalizePpmItemStatus>(s.Trim(), ignoreCase: true, out _))
                .WithMessage("Status must be one of 'Completed', 'Canceled', 'Closed' or 'Archived'.");
    }
}
