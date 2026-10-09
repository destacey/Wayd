using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Common.Domain.Identity;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.Settings;
using Wayd.Organization.Application.HolidayCalendars.Commands;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.TestData;
using Wayd.Tests.Shared;

namespace Wayd.Organization.Application.Tests.Sut.HolidayCalendars.Commands;

public class DeleteHolidayCalendarCommandHandlerTests : IDisposable
{
    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly Mock<ISettings<SchedulingSettings>> _schedulingSettings = new();
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Instant.FromUtc(2026, 10, 8, 0, 0)));
    private readonly DeleteHolidayCalendarCommandHandler _handler;
    private readonly HolidayCalendar _calendar = new HolidayCalendarFaker().Generate();

    public DeleteHolidayCalendarCommandHandlerTests()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns(SystemUser.Id);

        _schedulingSettings.Setup(s => s.Get(It.IsAny<CancellationToken>())).ReturnsAsync(new SchedulingSettings());
        _dbContext.AddHolidayCalendar(_calendar);

        _handler = new DeleteHolidayCalendarCommandHandler(
            _dbContext,
            _schedulingSettings.Object,
            _dateTimeProvider,
            currentUser.Object,
            NullLogger<DeleteHolidayCalendarCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_WhenUnused_DeletesTheCalendarAndRaisesEvent()
    {
        // Act
        var result = await _handler.Handle(new DeleteHolidayCalendarCommand(_calendar.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.HolidayCalendars.Should().BeEmpty();
        _calendar.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<HolidayCalendarDeletedEvent>();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenItIsTheSystemDefault_FailsWithoutDeleting()
    {
        // Arrange
        _schedulingSettings.Setup(s => s.Get(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SchedulingSettings { DefaultHolidayCalendarId = _calendar.Id });

        // Act
        var result = await _handler.Handle(new DeleteHolidayCalendarCommand(_calendar.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("system default");
        _dbContext.HolidayCalendars.Should().ContainSingle();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenAnOperatingModelUsesIt_FailsWithoutDeleting()
    {
        // Arrange
        var team = new TeamFaker().Generate();
        var model = team.SetOperatingModel(new LocalDate(2026, 1, 1), Methodology.Scrum, SizingMethod.StoryPoints, "UTC", 1,
            WorkingWeek.MondayToFriday, _calendar.Id, EventActor.System, _dateTimeProvider.Now).Value;
        _dbContext.AddTeamOperatingModel(model);

        // Act
        var result = await _handler.Handle(new DeleteHolidayCalendarCommand(_calendar.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("used by 1 team operating model");
        _dbContext.HolidayCalendars.Should().ContainSingle();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
