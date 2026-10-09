using FluentAssertions;
using Moq;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Application.SystemSettings.Scheduling.Queries;
using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.Tests.Sut.SystemSettings.Scheduling;

public class GetSchedulingSettingsQueryHandlerTests
{
    private readonly Mock<ISettings<SchedulingSettings>> _settings = new();
    private readonly Mock<IDispatcher> _dispatcher = new();

    private GetSchedulingSettingsQueryHandler CreateHandler() => new(_settings.Object, _dispatcher.Object);

    [Fact]
    public async Task Handle_WithADefaultHolidayCalendar_IncludesItsName()
    {
        // Arrange
        var calendarId = Guid.NewGuid();
        _settings.Setup(s => s.Get(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SchedulingSettings { DefaultHolidayCalendarId = calendarId });
        _dispatcher.Setup(d => d.Send(new GetHolidayCalendarNavigationQuery(calendarId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NavigationDto.Create(calendarId, 1, "United States"));

        // Act
        var result = await CreateHandler().Handle(new GetSchedulingSettingsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.DefaultHolidayCalendar.Should().Be(NavigationDto.Create(calendarId, 1, "United States"));
    }

    [Fact]
    public async Task Handle_WithNoDefaultHolidayCalendar_HasNoName()
    {
        // Arrange
        _settings.Setup(s => s.Get(It.IsAny<CancellationToken>())).ReturnsAsync(new SchedulingSettings());

        // Act
        var result = await CreateHandler().Handle(new GetSchedulingSettingsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.DefaultHolidayCalendar.Should().BeNull();
        _dispatcher.Verify(d => d.Send(It.IsAny<GetHolidayCalendarNavigationQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
