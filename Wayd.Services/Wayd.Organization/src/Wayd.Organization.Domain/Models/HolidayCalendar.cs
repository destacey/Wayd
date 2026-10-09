using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Common.Extensions;

namespace Wayd.Organization.Domain.Models;

/// <summary>
/// A named set of holidays, such as a country's public holidays. A team operating model points at one, or the
/// system default applies, and a sprint's ideal burn-down stays flat on its holidays.
/// </summary>
/// <remarks>
/// Not tied to a team: one calendar serves every team that shares those days off. A calendar holds only the days
/// a whole team is off, so a team spread across regions uses a calendar of the holidays its members share.
/// </remarks>
public sealed class HolidayCalendar : BaseAuditableEntity, IHasIdAndKey
{
    public const int NameMaxLength = 128;
    public const int DescriptionMaxLength = 1024;
    public const int HolidayNameMaxLength = 128;

    private readonly List<Holiday> _holidays = [];

    private HolidayCalendar() { }

    private HolidayCalendar(string name, string? description)
    {
        Name = name;
        Description = description;
    }

    /// <summary>The database-generated alternate key.</summary>
    public int Key { get; private init; }

    /// <summary>The calendar's name, unique across calendars.</summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = null!;

    /// <summary>What the calendar covers, such as the region or office.</summary>
    public string? Description
    {
        get;
        private set => field = value.NullIfWhiteSpacePlusTrim();
    }

    /// <summary>The calendar's holidays, at most one per date.</summary>
    public IReadOnlyCollection<Holiday> Holidays => _holidays.AsReadOnly();

    /// <summary>Renames the calendar or changes its description.</summary>
    public Result UpdateDetails(string name, string? description, EventActor actor, Instant timestamp)
    {
        var previousName = Name;
        var previousDescription = Description;

        Name = name;
        Description = description;

        if (Name == previousName && Description == previousDescription)
            return Result.Success();

        AddDomainEvent(new HolidayCalendarDetailsUpdatedEvent(Id, Key, Name, Description, previousName, previousDescription, actor, timestamp));
        return Result.Success();
    }

    /// <summary>Adds a holiday on <paramref name="date"/>. Fails when the calendar already has one that day.</summary>
    public Result<Holiday> AddHoliday(LocalDate date, string name, EventActor actor, Instant timestamp)
    {
        if (_holidays.Any(h => h.Date == date))
            return Result.Failure<Holiday>($"The calendar already has a holiday on {date:yyyy-MM-dd}.");

        var holiday = new Holiday(date, name);
        _holidays.Add(holiday);

        AddDomainEvent(new HolidayAddedEvent(Id, Key, holiday.Id, holiday.Date, holiday.Name, actor, timestamp));
        return holiday;
    }

    /// <summary>
    /// Moves a holiday to another date or renames it. Fails when another holiday is already on the new date.
    /// </summary>
    public Result ChangeHoliday(Guid holidayId, LocalDate date, string name, EventActor actor, Instant timestamp)
    {
        var holiday = _holidays.SingleOrDefault(h => h.Id == holidayId);
        if (holiday is null)
            return Result.Failure($"Holiday {holidayId} is not in this calendar.");

        if (_holidays.Any(h => h.Id != holidayId && h.Date == date))
            return Result.Failure($"The calendar already has a holiday on {date:yyyy-MM-dd}.");

        var previousDate = holiday.Date;
        var previousName = holiday.Name;

        holiday.Change(date, name);

        if (holiday.Date == previousDate && holiday.Name == previousName)
            return Result.Success();

        AddDomainEvent(new HolidayChangedEvent(Id, Key, holiday.Id, holiday.Date, holiday.Name, previousDate, previousName, actor, timestamp));
        return Result.Success();
    }

    /// <summary>Removes a holiday, so teams using the calendar work that day again.</summary>
    public Result RemoveHoliday(Guid holidayId, EventActor actor, Instant timestamp)
    {
        var holiday = _holidays.SingleOrDefault(h => h.Id == holidayId);
        if (holiday is null)
            return Result.Failure($"Holiday {holidayId} is not in this calendar.");

        _holidays.Remove(holiday);

        AddDomainEvent(new HolidayRemovedEvent(Id, Key, holiday.Id, holiday.Date, holiday.Name, actor, timestamp));
        return Result.Success();
    }

    /// <summary>
    /// Records that the calendar is being deleted. Checking that nothing uses it, and removing it, is the caller's.
    /// </summary>
    public void Delete(EventActor actor, Instant timestamp) =>
        AddDomainEvent(new HolidayCalendarDeletedEvent(Id, Key, Name, actor, timestamp));

    /// <summary>Creates a calendar with no holidays.</summary>
    public static HolidayCalendar Create(string name, string? description, EventActor actor, Instant timestamp)
    {
        var calendar = new HolidayCalendar(name, description);

        // Deferred because Key is database-generated: an event raised here would carry Key 0.
        var createdName = calendar.Name;
        var createdDescription = calendar.Description;
        calendar.AddPostPersistenceAction(() => calendar.AddDomainEvent(new HolidayCalendarCreatedEvent(
            calendar.Id,
            calendar.Key,
            createdName,
            createdDescription,
            actor,
            timestamp)));

        return calendar;
    }
}
