using NodaTime.Text;

namespace Wayd.Web.Api.Models.ProductManagement;

/// <summary>
/// Reads the instants a CSV import row carries as text.
/// </summary>
/// <remarks>
/// Each value must carry its offset — <c>2026-03-01T14:30:00Z</c> or <c>2026-03-01T09:30:00-05:00</c>. A
/// value with no offset is refused rather than read in the server's zone, which would shift every historical
/// record by whatever that zone happens to be.
/// </remarks>
internal static class OffsetTimestamp
{
    /// <summary>
    /// An ISO-8601 timestamp with its offset. Null for a blank cell, and for a value that does not parse —
    /// which the row's validator refuses through <see cref="IsValid"/>.
    /// </summary>
    public static Instant? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var parsed = OffsetDateTimePattern.ExtendedIso.Parse(value.Trim());

        return parsed.Success ? parsed.Value.ToInstant() : null;
    }

    /// <summary>Whether a non-blank value parses.</summary>
    public static bool IsValid(string? value) => Parse(value) is not null;

    /// <summary>The validation message for a value that does not parse.</summary>
    public static string Message(string column) =>
        $"{column} must be an ISO-8601 timestamp with an offset, such as 2026-03-01T14:30:00Z.";
}
