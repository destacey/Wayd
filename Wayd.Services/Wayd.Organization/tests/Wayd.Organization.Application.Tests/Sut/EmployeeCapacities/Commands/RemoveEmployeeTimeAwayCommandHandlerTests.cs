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

public class RemoveEmployeeTimeAwayCommandHandlerTests : IDisposable
{
    private static readonly LocalDate Feb1 = new(2027, 2, 1);
    private static readonly LocalDate Feb5 = new(2027, 2, 5);

    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Instant.FromUtc(2026, 10, 13, 9, 30)));
    private readonly Employee _manager;
    private readonly Employee _peer;

    public RemoveEmployeeTimeAwayCommandHandlerTests()
    {
        _manager = new EmployeeFaker().Generate();
        _peer = new EmployeeFaker().WithManagerId(_manager.Id).Generate();
        _dbContext.AddEmployees([_manager, _peer]);
    }

    private RemoveEmployeeTimeAwayCommandHandler CreateHandler(Guid? callerEmployeeId)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns(SystemUser.Id);

        return new RemoveEmployeeTimeAwayCommandHandler(
            _dbContext,
            currentUser.Object,
            TestPrincipal.LinkedTo(callerEmployeeId),
            _dateTimeProvider,
            NullLogger<RemoveEmployeeTimeAwayCommandHandler>.Instance);
    }

    /// <summary>An employee reporting to the manager, with one stretch of time away.</summary>
    private (Employee Employee, EmployeeCapacity Capacity, EmployeeTimeAway TimeAway) SeedAway(bool isActive = true)
    {
        var employee = new EmployeeFaker().WithManagerId(_manager.Id).WithIsActive(isActive).Generate();
        _dbContext.AddEmployee(employee);

        var capacity = EmployeeCapacity.Create(employee.Id);
        var timeAway = capacity.RecordTimeAway(Feb1, Feb5, 100, TimeAwayReason.PaidTimeOff, EventActor.System, _dateTimeProvider.Now).Value;
        capacity.ClearDomainEvents();
        _dbContext.AddEmployeeCapacity(capacity);

        return (employee, capacity, timeAway);
    }

    [Fact]
    public async Task Handle_ByTheEmployeeThemselves_RemovesTheTimeAway()
    {
        // Arrange
        var (employee, capacity, timeAway) = SeedAway();
        var handler = CreateHandler(employee.Id);

        // Act
        var result = await handler.Handle(new RemoveEmployeeTimeAwayCommand(employee.Id, timeAway.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        capacity.TimeAway.Should().BeEmpty();
        var raised = capacity.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployeeTimeAwayRemovedEvent>().Subject;
        raised.TimeAwayId.Should().Be(timeAway.Id);
        raised.Actor.EmployeeId.Should().Be(employee.Id);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ForAnInactiveEmployee_StillRemovesTheTimeAway()
    {
        // Arrange
        var (employee, capacity, timeAway) = SeedAway(isActive: false);
        var handler = CreateHandler(_manager.Id);

        // Act
        var result = await handler.Handle(new RemoveEmployeeTimeAwayCommand(employee.Id, timeAway.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        capacity.TimeAway.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ByAPeerWithoutTheAdministerPermission_FailsWithoutRemoving()
    {
        // Arrange
        var (employee, capacity, timeAway) = SeedAway();
        var handler = CreateHandler(_peer.Id);

        // Act
        var result = await handler.Handle(new RemoveEmployeeTimeAwayCommand(employee.Id, timeAway.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TimeAwayAuthorization.NotAllowedError);
        capacity.TimeAway.Should().ContainSingle();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForTimeAwayThatIsNotTheEmployees_FailsWithoutSaving()
    {
        // Arrange
        var (employee, capacity, _) = SeedAway();
        var handler = CreateHandler(employee.Id);

        // Act
        var result = await handler.Handle(new RemoveEmployeeTimeAwayCommand(employee.Id, Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        capacity.TimeAway.Should().ContainSingle();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForAnEmployeeThatDoesNotExist_SaysOnlyThatTheCallerMayNot()
    {
        // Arrange — a manager probing an id must not learn whether it is an employee
        var unknown = Guid.NewGuid();
        var handler = CreateHandler(_manager.Id);

        // Act
        var result = await handler.Handle(new RemoveEmployeeTimeAwayCommand(unknown, Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TimeAwayAuthorization.NotAllowedError);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
