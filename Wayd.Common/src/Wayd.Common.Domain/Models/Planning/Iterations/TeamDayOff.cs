using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;
using NodaTime;
using NodaTime.Text;

namespace Wayd.Common.Domain.Models.Planning.Iterations;

/// <summary>
/// A day within a sprint that the whole team is off for some or all of, beyond its working week and holidays —
/// an offsite, a hackathon. It takes its share of everyone's capacity that day, and the sprint's ideal burn-down
/// drops by that much less.
/// </summary>
/// <remarks>
/// Persisted as its <see cref="ToString"/> form and read back with <see cref="Parse"/>: the ISO date alone for a
/// full day, so a value written before part days existed reads as a full day off.
/// </remarks>
/// <param name="Date">The day the team is off.</param>
/// <param name="PercentOff">The share of the working day the team is off, from 1 to 100.</param>
public sealed record TeamDayOff(LocalDate Date, int PercentOff = TeamDayOff.FullDay)
{
    /// <summary>The percent off of a whole day.</summary>
    public const int FullDay = 100;

    private const char PercentSeparator = ':';

    /// <summary>Whether the team is off all of the day.</summary>
    [JsonIgnore]
    public bool IsFullDay => PercentOff >= FullDay;

    /// <summary>The share of the day the team still works, from 0 to 1.</summary>
    [JsonIgnore]
    public decimal ShareWorked => (FullDay - PercentOff) / (decimal)FullDay;

    /// <summary>Whether <paramref name="percentOff"/> is a share of a day a team can be off.</summary>
    public static bool IsValidPercent(int percentOff) => percentOff is >= 1 and <= FullDay;

    /// <summary>Reads a team day off back from its <see cref="ToString"/> form.</summary>
    /// <exception cref="FormatException">The value is not a date, optionally followed by a percent from 1 to 100.</exception>
    public static TeamDayOff Parse(string value) =>
        TryParse(value, out var day)
            ? day
            : throw new FormatException($"'{value}' is not a date, optionally followed by a percent off from 1 to 100.");

    /// <summary>Reads a team day off from its <see cref="ToString"/> form, failing instead of throwing.</summary>
    public static bool TryParse(string? value, [NotNullWhen(true)] out TeamDayOff? day)
    {
        day = null;
        var parts = value?.Split(PercentSeparator) ?? [];
        if (parts.Length is 0 or > 2)
            return false;

        var date = LocalDatePattern.Iso.Parse(parts[0].Trim());
        if (!date.Success)
            return false;

        var percentOff = FullDay;
        if (parts.Length == 2
            && (!int.TryParse(parts[1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out percentOff) || !IsValidPercent(percentOff)))
            return false;

        day = new TeamDayOff(date.Value, percentOff);
        return true;
    }

    /// <summary>The ISO date, followed by the percent off for a part day: <c>2026-10-01</c>, <c>2026-10-02:50</c>.</summary>
    public override string ToString() =>
        IsFullDay
            ? LocalDatePattern.Iso.Format(Date)
            : $"{LocalDatePattern.Iso.Format(Date)}{PercentSeparator}{PercentOff.ToString(CultureInfo.InvariantCulture)}";
}
