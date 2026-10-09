using NodaTime;

namespace Wayd.Common.Application.Requests.Organization;

/// <summary>
/// The holidays of an Organization holiday calendar from <paramref name="From"/> to <paramref name="To"/>, both
/// inclusive, in date order. A null <paramref name="HolidayCalendarId"/> reads the system default calendar, and
/// with no default, or a calendar that no longer exists, there are none.
/// </summary>
/// <remarks>
/// Read each time rather than copied: a change to a calendar, or to which calendar is the default, changes the
/// days off of every sprint that uses it.
/// </remarks>
public sealed record GetHolidayDatesQuery(Guid? HolidayCalendarId, LocalDate From, LocalDate To) : IQuery<IReadOnlyList<LocalDate>>;
