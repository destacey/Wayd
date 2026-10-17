using Moq;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Settings;
using NodaTime;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Organization.Application.EmployeeCapacities.Queries;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.TestData;

namespace Wayd.Organization.Application.Tests.Sut.EmployeeCapacities.Queries;

public class GetEmployeeTimeAwayQueryHandlerTests : IDisposable
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 13, 9, 30);
    private static readonly LocalDate Feb1 = new(2027, 2, 1);
    private static readonly LocalDate Mar1 = new(2027, 3, 1);

    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly Employee _manager;
    private readonly Employee _employee;
    private readonly Employee _teammate;
    private readonly EmployeeCapacity _capacity;

    public GetEmployeeTimeAwayQueryHandlerTests()
    {
        _manager = new EmployeeFaker().Generate();
        _employee = new EmployeeFaker().WithManagerId(_manager.Id).Generate();
        _teammate = new EmployeeFaker().WithManagerId(_manager.Id).Generate();
        _dbContext.AddEmployees([_manager, _employee, _teammate]);

        // Recorded out of date order, each with a different reason.
        _capacity = EmployeeCapacity.Create(_employee.Id);
        _capacity.RecordTimeAway(Mar1, Mar1.PlusDays(4), 100, TimeAwayReason.PaidTimeOff, EventActor.System, Now);
        _capacity.RecordTimeAway(Feb1, Feb1, 50, TimeAwayReason.Training, EventActor.System, Now);
        _dbContext.AddEmployeeCapacity(_capacity);
    }

    private GetEmployeeTimeAwayQueryHandler CreateHandler(Guid? callerEmployeeId, bool timeAwayAdministrator = false)
    {
        var settings = new Mock<ISettings<SchedulingSettings>>();
        settings.Setup(s => s.Get(It.IsAny<CancellationToken>())).ReturnsAsync(new SchedulingSettings { WorkingDayHours = 7.5m });
        return new(_dbContext, TestPrincipal.LinkedTo(callerEmployeeId, timeAwayAdministrator), settings.Object);
    }

    [Fact]
    public async Task Handle_GivesTheWorkingDayLengthTimeAwayIsAShareOf()
    {
        // Act
        var result = await CreateHandler(_teammate.Id).Handle(new GetEmployeeTimeAwayQuery(_employee.Id), TestContext.Current.CancellationToken);

        // Assert
        result!.WorkingDayHours.Should().Be(7.5m);
        result.OwnWorkingDayHours.Should().BeNull();
        result.SystemWorkingDayHours.Should().Be(7.5m);
    }

    [Fact]
    public async Task Handle_ForSomeoneWithTheirOwnWorkingDay_GivesTheirsAsTheLengthTimeAwayIsAShareOf()
    {
        // Arrange
        _capacity.ChangeWorkingDayHours(4m, EventActor.System, Now);

        // Act
        var result = await CreateHandler(_teammate.Id).Handle(new GetEmployeeTimeAwayQuery(_employee.Id), TestContext.Current.CancellationToken);

        // Assert
        result!.WorkingDayHours.Should().Be(4m);
        result.OwnWorkingDayHours.Should().Be(4m);
        result.SystemWorkingDayHours.Should().Be(7.5m);
    }

    [Fact]
    public async Task Handle_ForTheEmployeeThemselves_ReturnsTheirTimeAwayInDateOrderWithItsReasons()
    {
        // Arrange
        var handler = CreateHandler(_employee.Id);

        // Act
        var timeAway = await handler.Handle(new GetEmployeeTimeAwayQuery(_employee.Id), TestContext.Current.CancellationToken);

        // Assert
        timeAway.Should().NotBeNull();
        timeAway!.CanManage.Should().BeTrue();
        timeAway.Entries.Select(e => (e.Start, e.End, e.PercentAway, e.Reason)).Should().Equal(
            (Feb1, Feb1, 50, TimeAwayReason.Training),
            (Mar1, Mar1.PlusDays(4), 100, TimeAwayReason.PaidTimeOff));
        timeAway.Entries.Select(e => e.Id).Should().BeEquivalentTo(_capacity.TimeAway.Select(t => t.Id));
    }

    [Fact]
    public async Task Handle_ForTheirManager_ReturnsTheReasons()
    {
        // Arrange
        var handler = CreateHandler(_manager.Id);

        // Act
        var timeAway = await handler.Handle(new GetEmployeeTimeAwayQuery(_employee.Id), TestContext.Current.CancellationToken);

        // Assert
        timeAway!.CanManage.Should().BeTrue();
        timeAway.Entries.Select(e => e.Reason).Should().Equal(TimeAwayReason.Training, TimeAwayReason.PaidTimeOff);
    }

    [Fact]
    public async Task Handle_ForATeammate_ShowsThatTheyAreAwayButNotWhy()
    {
        // Arrange
        var handler = CreateHandler(_teammate.Id);

        // Act
        var timeAway = await handler.Handle(new GetEmployeeTimeAwayQuery(_employee.Id), TestContext.Current.CancellationToken);

        // Assert
        timeAway!.CanManage.Should().BeFalse();
        timeAway.Entries.Select(e => (e.Start, e.End, e.PercentAway)).Should().Equal(
            (Feb1, Feb1, 50),
            (Mar1, Mar1.PlusDays(4), 100));
        timeAway.Entries.Should().OnlyContain(e => e.Reason == null);
    }

    [Fact]
    public async Task Handle_ForACallerWithNoLinkedEmployee_HidesTheReasons()
    {
        // Arrange
        var handler = CreateHandler(null);

        // Act
        var timeAway = await handler.Handle(new GetEmployeeTimeAwayQuery(_employee.Id), TestContext.Current.CancellationToken);

        // Assert
        timeAway!.CanManage.Should().BeFalse();
        timeAway.Entries.Should().HaveCount(2).And.OnlyContain(e => e.Reason == null);
    }

    [Fact]
    public async Task Handle_ForATimeAwayAdministrator_ReturnsTheReasons()
    {
        // Arrange
        var handler = CreateHandler(_teammate.Id, timeAwayAdministrator: true);

        // Act
        var timeAway = await handler.Handle(new GetEmployeeTimeAwayQuery(_employee.Id), TestContext.Current.CancellationToken);

        // Assert
        timeAway!.CanManage.Should().BeTrue();
        timeAway.Entries.Select(e => e.Reason).Should().Equal(TimeAwayReason.Training, TimeAwayReason.PaidTimeOff);
    }

    [Fact]
    public async Task Handle_ReturnsTheEmployeesOwnHolidayCalendar()
    {
        // Arrange
        var calendar = new HolidayCalendarFaker().Generate();
        _dbContext.AddHolidayCalendar(calendar);
        _dbContext.AddHolidayCalendar(new HolidayCalendarFaker().Generate());
        _capacity.ChangeHolidayCalendar(calendar.Id, EventActor.System, Now);
        var handler = CreateHandler(_teammate.Id);

        // Act
        var timeAway = await handler.Handle(new GetEmployeeTimeAwayQuery(_employee.Id), TestContext.Current.CancellationToken);

        // Assert
        timeAway!.HolidayCalendar.Should().NotBeNull();
        timeAway.HolidayCalendar!.Id.Should().Be(calendar.Id);
        timeAway.HolidayCalendar.Key.Should().Be(calendar.Key);
        timeAway.HolidayCalendar.Name.Should().Be(calendar.Name);
    }

    [Fact]
    public async Task Handle_ForAnEmployeeWithNothingRecorded_ReturnsNoEntriesAndNoCalendar()
    {
        // Arrange
        var handler = CreateHandler(_manager.Id);

        // Act
        var timeAway = await handler.Handle(new GetEmployeeTimeAwayQuery(_teammate.Id), TestContext.Current.CancellationToken);

        // Assert
        timeAway.Should().NotBeNull();
        timeAway!.CanManage.Should().BeTrue();
        timeAway.HolidayCalendar.Should().BeNull();
        timeAway.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ForAnEmployeeThatDoesNotExist_ReturnsNull()
    {
        // Arrange
        var handler = CreateHandler(_manager.Id, timeAwayAdministrator: true);

        // Act
        var timeAway = await handler.Handle(new GetEmployeeTimeAwayQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        timeAway.Should().BeNull();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
