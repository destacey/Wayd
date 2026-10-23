namespace Wayd.Web.Api.Extensions;

/// <summary>
/// Reads a date-time bound from a query string as an instant. ASP.NET binds a value without a zone as
/// <see cref="DateTimeKind.Unspecified"/>; the API reads that as UTC rather than the server's local time.
/// </summary>
public static class UtcQueryTime
{
    /// <summary>The instant <paramref name="value"/> names, reading a value without a zone as UTC; null when absent.</summary>
    public static Instant? ToInstant(DateTime? value)
    {
        if (!value.HasValue)
            return null;

        var utc = value.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            : value.Value.ToUniversalTime();

        return Instant.FromDateTimeUtc(utc);
    }
}
