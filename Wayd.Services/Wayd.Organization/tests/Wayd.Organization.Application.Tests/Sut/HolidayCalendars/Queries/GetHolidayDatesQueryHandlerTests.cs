using Moq;
using NodaTime;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Settings;
using Wayd.Organization.Application.HolidayCalendars.Queries;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.TestData;

namespace Wayd.Organization.Application.Tests.Sut.HolidayCalendars.Queries;

public class GetHolidayDatesQueryHandlerTests : IDisposable
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 8, 0, 0);
    private static readonly LocalDate From = new(2026, 12, 1);
    private static readonly LocalDate To = new(2026, 12, 31);

    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly Mock<ISettings<SchedulingSettings>> _schedulingSettings = new();
    private readonly GetHolidayDatesQueryHandler _handler;
    private readonly HolidayCalendar _unitedStates = new HolidayCalendarFaker().Generate();
    private readonly HolidayCalendar _india = new HolidayCalendarFaker().Generate();

    public GetHolidayDatesQueryHandlerTests()
    {
        _unitedStates.AddHoliday(new LocalDate(2026, 11, 26), "Thanksgiving", EventActor.System, Now);
        _unitedStates.AddHoliday(new LocalDate(2026, 12, 25), "Christmas Day", EventActor.System, Now);
        _unitedStates.AddHoliday(new LocalDate(2026, 12, 24), "Christmas Eve", EventActor.System, Now);
        _india.AddHoliday(new LocalDate(2026, 12, 25), "Christmas", EventActor.System, Now);
        _india.AddHoliday(new LocalDate(2026, 11, 8), "Diwali", EventActor.System, Now);
        _dbContext.AddHolidayCalendar(_unitedStates);
        _dbContext.AddHolidayCalendar(_india);

        _schedulingSettings.Setup(s => s.Get(It.IsAny<CancellationToken>())).ReturnsAsync(new SchedulingSettings());
        _handler = new GetHolidayDatesQueryHandler(_dbContext, _schedulingSettings.Object);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_ForACalendar_ReturnsItsHolidaysInTheRangeInDateOrder()
    {
        // Act
        var dates = await _handler.Handle(new GetHolidayDatesQuery(_unitedStates.Id, From, To), TestContext.Current.CancellationToken);

        // Assert
        dates.Should().Equal(new LocalDate(2026, 12, 24), new LocalDate(2026, 12, 25));
    }

    [Fact]
    public async Task Handle_WithNoCalendar_ReadsTheSystemDefault()
    {
        // Arrange
        _schedulingSettings.Setup(s => s.Get(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SchedulingSettings { DefaultHolidayCalendarId = _india.Id });

        // Act
        var dates = await _handler.Handle(new GetHolidayDatesQuery(null, From, To), TestContext.Current.CancellationToken);

        // Assert
        dates.Should().Equal(new LocalDate(2026, 12, 25));
    }

    [Fact]
    public async Task Handle_WithNoCalendarAndNoDefault_ReturnsNone()
    {
        // Act
        var dates = await _handler.Handle(new GetHolidayDatesQuery(null, From, To), TestContext.Current.CancellationToken);

        // Assert
        dates.Should().BeEmpty();
    }
}
