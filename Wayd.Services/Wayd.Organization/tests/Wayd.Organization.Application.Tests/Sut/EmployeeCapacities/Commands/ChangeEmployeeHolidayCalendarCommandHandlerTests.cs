using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Common.Domain.Identity;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Organization.Application.EmployeeCapacities.Commands;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.Organization.Application.Tests.Sut.EmployeeCapacities.Commands;

public class ChangeEmployeeHolidayCalendarCommandHandlerTests : IDisposable
{
    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Instant.FromUtc(2026, 10, 13, 9, 30)));
    private readonly Employee _employee = new EmployeeFaker().Generate();
    private readonly HolidayCalendar _calendar = new HolidayCalendarFaker().Generate();
    private readonly Guid _callerEmployeeId = Guid.NewGuid();
    private readonly ChangeEmployeeHolidayCalendarCommandHandler _handler;

    public ChangeEmployeeHolidayCalendarCommandHandlerTests()
    {
        _dbContext.AddEmployee(_employee);
        _dbContext.AddHolidayCalendar(_calendar);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns(SystemUser.Id);

        _handler = new ChangeEmployeeHolidayCalendarCommandHandler(
            _dbContext,
            currentUser.Object,
            TestPrincipal.LinkedTo(_callerEmployeeId),
            _dateTimeProvider,
            NullLogger<ChangeEmployeeHolidayCalendarCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_GivesTheEmployeeTheCalendarAndRaisesEvent()
    {
        // Arrange
        var command = new ChangeEmployeeHolidayCalendarCommand(_employee.Id, _calendar.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var capacity = _dbContext.EmployeeCapacities.Should().ContainSingle().Subject;
        capacity.Id.Should().Be(_employee.Id);
        capacity.HolidayCalendarId.Should().Be(_calendar.Id);
        var raised = capacity.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployeeHolidayCalendarChangedEvent>().Subject;
        raised.Actor.EmployeeId.Should().Be(_callerEmployeeId);
        raised.Timestamp.Should().Be(_dateTimeProvider.Now);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithNoCalendar_GoesBackToTheTeamsCalendars()
    {
        // Arrange
        var capacity = EmployeeCapacity.Create(_employee.Id);
        capacity.ChangeHolidayCalendar(_calendar.Id, EventActor.System, _dateTimeProvider.Now);
        capacity.ClearDomainEvents();
        _dbContext.AddEmployeeCapacity(capacity);

        // Act
        var result = await _handler.Handle(new ChangeEmployeeHolidayCalendarCommand(_employee.Id, null), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        capacity.HolidayCalendarId.Should().BeNull();
        _dbContext.EmployeeCapacities.Should().ContainSingle();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ToTheCalendarTheyAlreadyHave_SavesNothing()
    {
        // Arrange
        var capacity = EmployeeCapacity.Create(_employee.Id);
        capacity.ChangeHolidayCalendar(_calendar.Id, EventActor.System, _dateTimeProvider.Now);
        capacity.ClearDomainEvents();
        _dbContext.AddEmployeeCapacity(capacity);

        // Act
        var result = await _handler.Handle(new ChangeEmployeeHolidayCalendarCommand(_employee.Id, _calendar.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithNoCalendarForAnEmployeeWithNoRecord_LeavesNoEmptyRecordBehind()
    {
        // Arrange
        var command = new ChangeEmployeeHolidayCalendarCommand(_employee.Id, null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _dbContext.EmployeeCapacities.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForACalendarThatDoesNotExist_FailsWithoutSaving()
    {
        // Arrange
        var unknown = Guid.NewGuid();

        // Act
        var result = await _handler.Handle(new ChangeEmployeeHolidayCalendarCommand(_employee.Id, unknown), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be($"Holiday calendar {unknown} not found.");
        _dbContext.EmployeeCapacities.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForAnEmployeeThatDoesNotExist_FailsWithoutSaving()
    {
        // Arrange
        var unknown = Guid.NewGuid();

        // Act
        var result = await _handler.Handle(new ChangeEmployeeHolidayCalendarCommand(unknown, _calendar.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be($"Employee {unknown} not found.");
        _dbContext.EmployeeCapacities.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
