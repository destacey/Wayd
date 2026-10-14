using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Common.Domain.Identity;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Organization.Application.EmployeeCapacities;
using Wayd.Organization.Application.EmployeeCapacities.Commands;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Models;
using Wayd.Tests.Shared;

namespace Wayd.Organization.Application.Tests.Sut.EmployeeCapacities.Commands;

public class RecordEmployeeTimeAwayCommandHandlerTests : IDisposable
{
    private static readonly LocalDate Feb1 = new(2027, 2, 1);
    private static readonly LocalDate Feb5 = new(2027, 2, 5);

    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Instant.FromUtc(2026, 10, 13, 9, 30)));
    private readonly Employee _manager;
    private readonly Employee _employee;
    private readonly Employee _peer;

    public RecordEmployeeTimeAwayCommandHandlerTests()
    {
        _manager = new EmployeeFaker().Generate();
        _employee = new EmployeeFaker().WithManagerId(_manager.Id).Generate();
        _peer = new EmployeeFaker().WithManagerId(_manager.Id).Generate();
        _dbContext.AddEmployees([_manager, _employee, _peer]);
    }

    private RecordEmployeeTimeAwayCommandHandler CreateHandler(Guid? callerEmployeeId, bool timeAwayAdministrator = false)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns(SystemUser.Id);

        return new RecordEmployeeTimeAwayCommandHandler(
            _dbContext,
            currentUser.Object,
            TestPrincipal.LinkedTo(callerEmployeeId, timeAwayAdministrator),
            _dateTimeProvider,
            NullLogger<RecordEmployeeTimeAwayCommandHandler>.Instance);
    }

    private RecordEmployeeTimeAwayCommand Command(Guid? employeeId = null) =>
        new(employeeId ?? _employee.Id, Feb1, Feb5, 50, TimeAwayReason.Training);

    [Fact]
    public async Task Handle_ByTheEmployeeThemselves_RecordsTheTimeAwayAndReturnsItsId()
    {
        // Arrange
        var handler = CreateHandler(_employee.Id);

        // Act
        var result = await handler.Handle(Command(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var capacity = _dbContext.EmployeeCapacities.Should().ContainSingle().Subject;
        capacity.Id.Should().Be(_employee.Id);
        var timeAway = capacity.TimeAway.Should().ContainSingle().Subject;
        result.Value.Should().Be(timeAway.Id);
        timeAway.Start.Should().Be(Feb1);
        timeAway.End.Should().Be(Feb5);
        timeAway.PercentAway.Should().Be(50);
        timeAway.Reason.Should().Be(TimeAwayReason.Training);
        var raised = capacity.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployeeTimeAwayRecordedEvent>().Subject;
        raised.Timestamp.Should().Be(_dateTimeProvider.Now);
        raised.Actor.EmployeeId.Should().Be(_employee.Id);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ByTheirManager_RecordsTheTimeAway()
    {
        // Arrange
        var handler = CreateHandler(_manager.Id);

        // Act
        var result = await handler.Handle(Command(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _dbContext.EmployeeCapacities.Single().TimeAway.Should().ContainSingle();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ByAPeerWithoutTheAdministerPermission_FailsWithoutRecording()
    {
        // Arrange
        var handler = CreateHandler(_peer.Id);

        // Act
        var result = await handler.Handle(Command(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TimeAwayAuthorization.NotAllowedError);
        _dbContext.EmployeeCapacities.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ByAPeerWithTheAdministerPermission_RecordsTheTimeAway()
    {
        // Arrange
        var handler = CreateHandler(_peer.Id, timeAwayAdministrator: true);

        // Act
        var result = await handler.Handle(Command(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _dbContext.EmployeeCapacities.Single().TimeAway.Should().ContainSingle();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_AddsToTheTimeAwayAlreadyRecorded()
    {
        // Arrange
        var capacity = EmployeeCapacity.Create(_employee.Id);
        capacity.RecordTimeAway(Feb1, Feb5, 50, TimeAwayReason.PaidTimeOff, EventActor.System, _dateTimeProvider.Now);
        capacity.ClearDomainEvents();
        _dbContext.AddEmployeeCapacity(capacity);
        var handler = CreateHandler(_employee.Id);

        // Act
        var result = await handler.Handle(Command(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _dbContext.EmployeeCapacities.Should().ContainSingle().Which.TimeAway.Should().HaveCount(2);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenADayWouldBeMoreThanAWholeDayAway_FailsWithoutSaving()
    {
        // Arrange
        var capacity = EmployeeCapacity.Create(_employee.Id);
        capacity.RecordTimeAway(Feb1, Feb5, 60, TimeAwayReason.PaidTimeOff, EventActor.System, _dateTimeProvider.Now);
        _dbContext.AddEmployeeCapacity(capacity);
        var handler = CreateHandler(_employee.Id);

        // Act
        var result = await handler.Handle(Command(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("110%");
        capacity.TimeAway.Should().ContainSingle();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForAnInactiveEmployee_FailsWithoutRecording()
    {
        // Arrange
        var leaver = new EmployeeFaker().AsInactive().Generate();
        _dbContext.AddEmployee(leaver);
        var handler = CreateHandler(leaver.Id);

        // Act
        var result = await handler.Handle(Command(leaver.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Time away cannot be recorded for an inactive employee.");
        _dbContext.EmployeeCapacities.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ByACallerWhoMayNotForAnEmployeeThatDoesNotExist_SaysOnlyThatTheyMayNot()
    {
        // Arrange — the answer must be the same as for a real employee, so ids cannot be probed
        var handler = CreateHandler(_peer.Id);

        // Act
        var unknown = await handler.Handle(Command(Guid.NewGuid()), TestContext.Current.CancellationToken);
        var real = await handler.Handle(Command(), TestContext.Current.CancellationToken);

        // Assert
        unknown.Error.Should().Be(TimeAwayAuthorization.NotAllowedError);
        real.Error.Should().Be(unknown.Error);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForAnEmployeeThatDoesNotExist_FailsWithoutRecording()
    {
        // Arrange
        var unknown = Guid.NewGuid();
        var handler = CreateHandler(_manager.Id, timeAwayAdministrator: true);

        // Act
        var result = await handler.Handle(Command(unknown), TestContext.Current.CancellationToken);

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
