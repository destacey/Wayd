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

public class ChangeEmployeeTimeAwayCommandHandlerTests : IDisposable
{
    private static readonly LocalDate Feb1 = new(2027, 2, 1);
    private static readonly LocalDate Feb5 = new(2027, 2, 5);

    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Instant.FromUtc(2026, 10, 13, 9, 30)));
    private readonly Employee _manager;
    private readonly Employee _employee;
    private readonly Employee _peer;
    private readonly EmployeeCapacity _capacity;
    private readonly EmployeeTimeAway _timeAway;

    public ChangeEmployeeTimeAwayCommandHandlerTests()
    {
        _manager = new EmployeeFaker().Generate();
        _employee = new EmployeeFaker().WithManagerId(_manager.Id).Generate();
        _peer = new EmployeeFaker().WithManagerId(_manager.Id).Generate();
        _dbContext.AddEmployees([_manager, _employee, _peer]);

        _capacity = EmployeeCapacity.Create(_employee.Id);
        _timeAway = _capacity.RecordTimeAway(Feb1, Feb5, 100, TimeAwayReason.PaidTimeOff, EventActor.System, _dateTimeProvider.Now).Value;
        _capacity.ClearDomainEvents();
        _dbContext.AddEmployeeCapacity(_capacity);
    }

    private ChangeEmployeeTimeAwayCommandHandler CreateHandler(Guid? callerEmployeeId, bool timeAwayAdministrator = false)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns(SystemUser.Id);

        return new ChangeEmployeeTimeAwayCommandHandler(
            _dbContext,
            currentUser.Object,
            TestPrincipal.LinkedTo(callerEmployeeId, timeAwayAdministrator),
            _dateTimeProvider,
            NullLogger<ChangeEmployeeTimeAwayCommandHandler>.Instance);
    }

    private ChangeEmployeeTimeAwayCommand Command(Guid? timeAwayId = null) =>
        new(_employee.Id, timeAwayId ?? _timeAway.Id, Feb1.PlusDays(1), Feb5.PlusDays(1), 50, TimeAwayReason.Training);

    [Fact]
    public async Task Handle_ByTheirManager_ChangesTheTimeAway()
    {
        // Arrange
        var handler = CreateHandler(_manager.Id);

        // Act
        var result = await handler.Handle(Command(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _timeAway.Start.Should().Be(Feb1.PlusDays(1));
        _timeAway.End.Should().Be(Feb5.PlusDays(1));
        _timeAway.PercentAway.Should().Be(50);
        _timeAway.Reason.Should().Be(TimeAwayReason.Training);
        var raised = _capacity.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployeeTimeAwayChangedEvent>().Subject;
        raised.Actor.EmployeeId.Should().Be(_manager.Id);
        raised.Timestamp.Should().Be(_dateTimeProvider.Now);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ByAPeerWithoutTheAdministerPermission_FailsWithoutChanging()
    {
        // Arrange
        var handler = CreateHandler(_peer.Id);

        // Act
        var result = await handler.Handle(Command(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TimeAwayAuthorization.NotAllowedError);
        _timeAway.Start.Should().Be(Feb1);
        _timeAway.Reason.Should().Be(TimeAwayReason.PaidTimeOff);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ToWhatItAlreadyIs_SavesNothing()
    {
        // Arrange
        var handler = CreateHandler(_employee.Id);
        var command = new ChangeEmployeeTimeAwayCommand(_employee.Id, _timeAway.Id, Feb1, Feb5, 100, TimeAwayReason.PaidTimeOff);

        // Act
        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _capacity.DomainEvents.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForTimeAwayThatIsNotTheEmployees_FailsWithoutSaving()
    {
        // Arrange
        var unknown = Guid.NewGuid();
        var handler = CreateHandler(_employee.Id);

        // Act
        var result = await handler.Handle(Command(unknown), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be($"Time away {unknown} is not this employee's.");
        _timeAway.Start.Should().Be(Feb1);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForAnInactiveEmployee_ChangesTheTimeAway()
    {
        // Arrange — a leaver's history can still be corrected
        var leaver = new EmployeeFaker().AsInactive().WithManagerId(_manager.Id).Generate();
        _dbContext.AddEmployee(leaver);
        var capacity = EmployeeCapacity.Create(leaver.Id);
        var timeAway = capacity.RecordTimeAway(Feb1, Feb5, 100, TimeAwayReason.PaidTimeOff, EventActor.System, _dateTimeProvider.Now).Value;
        _dbContext.AddEmployeeCapacity(capacity);
        var handler = CreateHandler(_manager.Id);
        var command = new ChangeEmployeeTimeAwayCommand(leaver.Id, timeAway.Id, Feb1, Feb1, 50, TimeAwayReason.Other);

        // Act
        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        timeAway.End.Should().Be(Feb1);
        timeAway.PercentAway.Should().Be(50);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
