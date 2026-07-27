namespace Wayd.Common.Application.Validation;

// The colour format Wayd accepts wherever a user picks a colour — roadmap items and palettes, story
// map personas, and anything added later. Three- or six-digit hex, which is what the Ant Design
// colour pickers emit.
//
// Shared so the rule cannot drift between features: a length limit alone would accept "red" or
// "purple7", which store happily and then render as no colour at all.
public static class HexColorValidatorExtension
{
    private const string Pattern = "^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$";

    /// <summary>
    /// Requires a valid hex colour code (<c>#RGB</c> or <c>#RRGGBB</c>). Does not imply
    /// <c>NotEmpty()</c> — an optional colour should be wrapped in <c>When(...)</c>, and a required
    /// one should chain <c>NotEmpty()</c> first.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> IsHexColor<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Matches(Pattern)
            .WithMessage("Color must be a valid hex color code.");
}
