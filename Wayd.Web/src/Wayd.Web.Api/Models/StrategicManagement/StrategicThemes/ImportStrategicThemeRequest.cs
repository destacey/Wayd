using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.StrategicManagement.Application.StrategicThemes.Dtos;

namespace Wayd.Web.Api.Models.StrategicManagement.StrategicThemes;

/// <summary>
/// A single CSV row for the strategic theme import. <see cref="State"/> is the theme's state on creation
/// (case-insensitive: "Proposed" / "Active" / "Archived"), applied directly rather than through the
/// activate/archive transitions.
/// </summary>
public sealed class ImportStrategicThemeRequest
{
    /// <summary>
    /// The caller's own key for this row, unique within the file (case-insensitively). Results are
    /// reported against it. Falls back to the row's position when the column is absent, so a
    /// hand-authored file still works.
    /// </summary>
    public string? ImportId { get; set; }

    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;

    /// <summary>The theme's state. Defaults to Active when the column is absent.</summary>
    public string State { get; set; } = nameof(StrategicThemeState.Active);

    public ImportStrategicThemeDto ToImportStrategicThemeDto()
    {
        var state = Enum.Parse<StrategicThemeState>(State.Trim(), ignoreCase: true);

        return new ImportStrategicThemeDto(Name, Description, state);
    }
}

public sealed class ImportStrategicThemeRequestValidator : CustomValidator<ImportStrategicThemeRequest>
{
    public ImportStrategicThemeRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(t => t.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(t => t.Description)
            .NotEmpty()
            .MaximumLength(1024);

        RuleFor(t => t.State)
            .NotEmpty()
            .Must(s => Enum.TryParse<StrategicThemeState>(s.Trim(), ignoreCase: true, out _))
                .WithMessage("State must be one of 'Proposed', 'Active' or 'Archived'.");
    }
}
