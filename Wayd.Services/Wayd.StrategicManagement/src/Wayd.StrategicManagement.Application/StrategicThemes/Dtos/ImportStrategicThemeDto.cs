using Wayd.Common.Application.Validation;
using Wayd.Common.Domain.Enums.StrategicManagement;

namespace Wayd.StrategicManagement.Application.StrategicThemes.Dtos;

/// <summary>
/// A single strategic theme row. Themes are referenced by other imports (programs and projects) by
/// <see cref="Name"/>, so names must be unique within the batch and against existing themes.
/// The <see cref="State"/> is applied on creation rather than through the activate/archive transitions,
/// because <c>StrategicTheme.Create</c> accepts the state directly.
/// </summary>
public sealed record ImportStrategicThemeDto(
    string Name,
    string Description,
    StrategicThemeState State);

public sealed class ImportStrategicThemeDtoValidator : CustomValidator<ImportStrategicThemeDto>
{
    public ImportStrategicThemeDtoValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(t => t.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(t => t.Description)
            .NotEmpty()
            .MaximumLength(1024);

        RuleFor(t => t.State)
            .IsInEnum();
    }
}
