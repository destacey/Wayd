using NodaTime;

namespace Wayd.Common.Extensions;

public static class DateOnlyExtensions
{
    /// <summary>
    /// Converts a <see cref="DateOnly"/> to the <see cref="LocalDate"/> the domain models dates with.
    /// </summary>
    /// <remarks>
    /// The inverse of <see cref="LocalDate.ToDateOnly"/>. Both types mean the same thing — a date with no
    /// time and no zone — so the conversion is total, which is what makes <c>DateOnly</c> the right type
    /// for a date-only API field: nothing has to invent a time of day on the way in.
    /// </remarks>
    public static LocalDate ToLocalDate(this DateOnly date) => LocalDate.FromDateOnly(date);
}
