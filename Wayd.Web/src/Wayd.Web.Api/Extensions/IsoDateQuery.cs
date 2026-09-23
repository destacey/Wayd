using NodaTime.Text;

namespace Wayd.Web.Api.Extensions;

/// <summary>
/// Calendar dates in query strings are yyyy-MM-dd strings, not DateTimes: the generated client
/// would send a Date as a UTC timestamp, which lands on the previous day for a browser east of UTC.
/// </summary>
public static class IsoDateQuery
{
    public const string FormatError = "Dates must be in yyyy-MM-dd format.";

    /// <returns>False when a value is given but is not a yyyy-MM-dd date.</returns>
    public static bool TryParse(string? value, out LocalDate? date)
    {
        date = null;
        if (string.IsNullOrWhiteSpace(value))
            return true;

        var parsed = LocalDatePattern.Iso.Parse(value);
        if (!parsed.Success)
            return false;

        date = parsed.Value;
        return true;
    }
}
