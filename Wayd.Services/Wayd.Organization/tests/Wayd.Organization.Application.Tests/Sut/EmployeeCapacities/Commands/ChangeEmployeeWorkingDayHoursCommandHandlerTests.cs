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

public class ChangeEmployeeWorkingDayHoursCommandHandlerTests : IDisposable
{
    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Instant.FromUtc(2026, 10, 17, 9, 30)));
    private readonly Employee _employee = new EmployeeFaker().Generate();
    private readonly Guid _callerEmployeeId = Guid.NewGuid();
    private readonly ChangeEmployeeWorkingDayHoursCommandHandler _handler;

    public ChangeEmployeeWorkingDayHoursCommandHandlerTests()
    {
        _dbContext.AddEmployee(_employee);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns(SystemUser.Id);

        _handler = new ChangeEmployeeWorkingDayHoursCommandHandler(
            _dbContext,
            currentUser.Object,
            TestPrincipal.LinkedTo(_callerEmployeeId),
            _dateTimeProvider,
            NullLogger<ChangeEmployeeWorkingDayHoursCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_GivesTheEmployeeTheirWorkingDayAndRaisesEvent()
    {
        // Arrange
        var command = new ChangeEmployeeWorkingDayHoursCommand(_employee.Id, 4m);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var capacity = _dbContext.EmployeeCapacities.Should().ContainSingle().Subject;
        capacity.Id.Should().Be(_employee.Id);
        capacity.WorkingDayHours.Should().Be(4m);
        var raised = capacity.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployeeWorkingDayHoursChangedEvent>().Subject;
        raised.Actor.EmployeeId.Should().Be(_callerEmployeeId);
        raised.Timestamp.Should().Be(_dateTimeProvider.Now);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithNoHours_GivesThemTheSystemsWorkingDay()
    {
        // Arrange
        var capacity = EmployeeCapacity.Create(_employee.Id);
        capacity.ChangeWorkingDayHours(4m, EventActor.System, _dateTimeProvider.Now);
        capacity.ClearDomainEvents();
        _dbContext.AddEmployeeCapacity(capacity);

        // Act
        var result = await _handler.Handle(new ChangeEmployeeWorkingDayHoursCommand(_employee.Id, null), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        capacity.WorkingDayHours.Should().BeNull();
        _dbContext.EmployeeCapacities.Should().ContainSingle();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ToTheHoursTheyAlreadyHave_SavesNothing()
    {
        // Arrange
        var capacity = EmployeeCapacity.Create(_employee.Id);
        capacity.ChangeWorkingDayHours(4m, EventActor.System, _dateTimeProvider.Now);
        capacity.ClearDomainEvents();
        _dbContext.AddEmployeeCapacity(capacity);

        // Act
        var result = await _handler.Handle(new ChangeEmployeeWorkingDayHoursCommand(_employee.Id, 4m), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithNoHoursForAnEmployeeWithNoRecord_LeavesNoEmptyRecordBehind()
    {
        // Arrange
        var command = new ChangeEmployeeWorkingDayHoursCommand(_employee.Id, null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _dbContext.EmployeeCapacities.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithMoreHoursThanADayHas_FailsWithoutSaving()
    {
        // Arrange
        var command = new ChangeEmployeeWorkingDayHoursCommand(_employee.Id, 25m);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.EmployeeCapacities.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForAnEmployeeThatDoesNotExist_FailsWithoutSaving()
    {
        // Arrange
        var unknown = Guid.NewGuid();

        // Act
        var result = await _handler.Handle(new ChangeEmployeeWorkingDayHoursCommand(unknown, 4m), TestContext.Current.CancellationToken);

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
