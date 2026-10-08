using Ardalis.GuardClauses;
using NodaTime;

namespace Wayd.Organization.Domain.Models;

/// <summary>
/// A day off in a <see cref="HolidayCalendar"/>: no team using the calendar works it.
/// </summary>
public sealed class Holiday : BaseAuditableEntity
{
    private Holiday() { }

    internal Holiday(LocalDate date, string name)
    {
        Date = date;
        Name = name;
    }

    /// <summary>The day off. A calendar holds at most one holiday per date.</summary>
    public LocalDate Date { get; private set; }

    /// <summary>What the day is, such as "New Year's Day".</summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = null!;

    internal void Change(LocalDate date, string name)
    {
        Date = date;
        Name = name;
    }
}
