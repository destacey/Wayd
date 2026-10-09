using Wayd.Common.Domain.Models.Organizations;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Application.SystemSettings.Scheduling.Commands;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.Tests.Sut.SystemSettings.Scheduling;

public class UpdateSchedulingSettingsCommandHandlerTests
{
    private readonly Mock<ISystemSettingsStore> _store = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly Mock<ISettings<SchedulingSettings>> _settings = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Guid _employeeId = Guid.NewGuid();

    public UpdateSchedulingSettingsCommandHandlerTests()
    {
        _currentUser.Setup(u => u.GetUserId()).Returns("admin-1");
        _currentUser.Setup(u => u.GetEmployeeId()).Returns(_employeeId);
        _settings.Setup(s => s.Get(It.IsAny<CancellationToken>())).ReturnsAsync(new SchedulingSettings());
    }

    private UpdateSchedulingSettingsCommandHandler CreateHandler() =>
        new(_store.Object, _settings.Object, _dispatcher.Object, _currentUser.Object, NullLogger<UpdateSchedulingSettingsCommandHandler>.Instance);

    [Fact]
    public async Task Handle_SavesTheTrimmedValuesAsTheCurrentUser()
    {
        // Arrange
        _store.Setup(s => s.Save(It.IsAny<SchedulingSettings>(), It.IsAny<EventActor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await CreateHandler().Handle(
            new UpdateSchedulingSettingsCommand(" Europe/London ", 3, WorkingWeek.MondayToFriday.Days, null), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _store.Verify(s => s.Save(
            new SchedulingSettings { DefaultTimeZone = "Europe/London", DefaultCommitmentGraceDays = 3 },
            EventActor.User("admin-1", _employeeId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsTheStoresFailure()
    {
        // Arrange
        _store.Setup(s => s.Save(It.IsAny<SchedulingSettings>(), It.IsAny<EventActor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("'Mars/Olympus_Mons' is not a valid IANA time zone."));

        // Act
        var result = await CreateHandler().Handle(
            new UpdateSchedulingSettingsCommand("Mars/Olympus_Mons", 1, WorkingWeek.MondayToFriday.Days, null), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not a valid IANA time zone");
    }

    [Fact]
    public async Task Handle_WithoutWorkingDays_KeepsTheSavedOnes()
    {
        // Arrange
        IsoDayOfWeek[] saved = [IsoDayOfWeek.Sunday, IsoDayOfWeek.Monday, IsoDayOfWeek.Tuesday, IsoDayOfWeek.Wednesday, IsoDayOfWeek.Thursday];
        _settings.Setup(s => s.Get(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SchedulingSettings { DefaultWorkingDays = saved });
        _store.Setup(s => s.Save(It.IsAny<SchedulingSettings>(), It.IsAny<EventActor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await CreateHandler().Handle(
            new UpdateSchedulingSettingsCommand("UTC", 1, null, null), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _store.Verify(s => s.Save(
            It.Is<SchedulingSettings>(v => v.DefaultWorkingDays.SequenceEqual(WorkingWeek.Create(saved).Value.Days)),
            It.IsAny<EventActor>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithAnUnknownDefaultHolidayCalendar_FailsWithoutSaving()
    {
        // Arrange
        var calendarId = Guid.NewGuid();
        _dispatcher.Setup(d => d.Send(new GetHolidayCalendarNavigationQuery(calendarId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NavigationDto?)null);

        // Act
        var result = await CreateHandler().Handle(
            new UpdateSchedulingSettingsCommand("UTC", 1, WorkingWeek.MondayToFriday.Days, calendarId), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _store.Verify(s => s.Save(It.IsAny<SchedulingSettings>(), It.IsAny<EventActor>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithAnExistingDefaultHolidayCalendar_SavesIt()
    {
        // Arrange
        var calendarId = Guid.NewGuid();
        _dispatcher.Setup(d => d.Send(new GetHolidayCalendarNavigationQuery(calendarId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NavigationDto.Create(calendarId, 1, "United States"));
        _store.Setup(s => s.Save(It.IsAny<SchedulingSettings>(), It.IsAny<EventActor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await CreateHandler().Handle(
            new UpdateSchedulingSettingsCommand("UTC", 1, WorkingWeek.MondayToFriday.Days, calendarId), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _store.Verify(s => s.Save(
            It.Is<SchedulingSettings>(v => v.DefaultHolidayCalendarId == calendarId),
            It.IsAny<EventActor>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
