using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;

namespace Wayd.Common.Domain.Scoring;

/// <summary>
/// Validation for scoring tokens — the short identifiers (e.g. "BV", "JS") that criteria and outputs
/// expose for use in formulas. A token must be a valid expression identifier so it can be referenced
/// unambiguously: a letter or underscore followed by letters, digits, or underscores.
/// </summary>
public static partial class ScoringToken
{
    public const int MaxLength = 32;

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();

    public static bool IsValid(string token) =>
        !string.IsNullOrWhiteSpace(token)
        && token.Length <= MaxLength
        && TokenPattern().IsMatch(token);

    public static Result Validate(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Result.Failure("Token must not be empty.");
        }

        if (token.Length > MaxLength)
        {
            return Result.Failure($"Token must not exceed {MaxLength} characters.");
        }

        return TokenPattern().IsMatch(token)
            ? Result.Success()
            : Result.Failure($"Token '{token}' is invalid. Tokens must start with a letter or underscore and contain only letters, digits, or underscores.");
    }
}
