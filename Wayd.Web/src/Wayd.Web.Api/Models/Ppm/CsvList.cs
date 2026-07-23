namespace Wayd.Web.Api.Models.Ppm;

/// <summary>
/// Parses the multi-value columns used by the PPM CSV imports — role assignments (employee numbers),
/// strategic theme names, project keys and the like. A CSV cell cannot carry a list, so these columns hold
/// their values separated by semicolons (commas would collide with the field delimiter).
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
}
