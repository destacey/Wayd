using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Organization;

namespace Wayd.Work.IntegrationTests.Infrastructure;

/// <summary>
/// A dispatcher standing in for the Organization module's holiday calendars, which these tests do not seed.
/// </summary>
internal static class HolidayDispatcher
{
    /// <summary>A dispatcher whose default holiday calendar holds <paramref name="holidays"/>, and no others.</summary>
    public static Mock<IDispatcher> With(params LocalDate[] holidays)
    {
        var dispatcher = new Mock<IDispatcher>();
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetHolidayDatesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetHolidayDatesQuery q, CancellationToken _) =>
                q.HolidayCalendarId is null ? [.. holidays.Where(h => h >= q.From && h <= q.To)] : []);
        return dispatcher;
    }
}
