namespace Wayd.Web.Api.Models;

/// <summary>
/// Parses the multi-value columns used by the CSV imports — role assignments (employee numbers),
/// strategic theme names, project keys, additional employee emails and the like. A CSV cell cannot carry a
/// list, so these columns hold their values separated by semicolons (commas would collide with the field
/// delimiter).
/// </summary>
public static class CsvList
{
    private static readonly char[] _separators = [';'];

    /// <summary>
    /// Splits a semicolon-separated column into its trimmed, non-empty values, preserving order and
    /// dropping duplicates. Returns an empty list when the column is absent or blank.
    /// </summary>
    public static IReadOnlyList<string> Split(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        return [.. value
            .Split(_separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// Splits a semicolon-separated column of ids. A value that is not a GUID is dropped rather than
    /// throwing: the request validator reports the malformed cell, which names the row it came from.
    /// </summary>
    public static IReadOnlyList<Guid> SplitIds(string? value) =>
        [.. Split(value).Select(v => Guid.TryParse(v, out var id) ? id : (Guid?)null).Where(id => id.HasValue).Select(id => id!.Value)];

    /// <summary>Whether every value in a semicolon-separated column parses as a GUID.</summary>
    public static bool AreAllIds(string? value) => Split(value).All(v => Guid.TryParse(v, out _));
}
