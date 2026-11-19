namespace Wayd.Common.Application.Interfaces;

/// <summary>
/// The current moment. A calendar day is never read from it directly: <see cref="ISystemCalendar"/> names the day
/// a moment falls on in the system time zone.
/// </summary>
public interface IDateTimeProvider : IScopedService
{
    /// <summary>The current instant.</summary>
    Instant Now { get; }
}
