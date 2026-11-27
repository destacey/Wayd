using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Employees.Commands;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Common.Models;
using Wayd.Tests.Shared;

namespace Wayd.Common.Application.Tests.Sut.Employees.Commands;

public sealed class DeactivateEmployeesPastAccessEndCommandHandlerTests
{
    // 05:00 UTC on 2 June is still 1 June in Honolulu, the test calendar's zone.
    private static readonly Instant Now = Instant.FromUtc(2026, 6, 2, 5, 0);
    private static readonly LocalDate SystemToday = new(2026, 6, 1);

    private readonly FakeWaydDbContext _dbContext = new();
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Now));

    private DeactivateEmployeesPastAccessEndCommandHandler CreateHandler() =>
        new(_dbContext, new TestingSystemCalendar(_dateTimeProvider), _dateTimeProvider,
            NullLogger<DeactivateEmployeesPastAccessEndCommandHandler>.Instance);

    private Employee Owned(string number, LocalDate? accessEndDate, bool isActive = true)
    {
        var employee = Employee.Create(
            new PersonName("Owned", null, number), number, null, new EmailAddress($"{number}@acme.example"),
            null, null, null, null, isActive, null, EventActor.System, Now, accessEndDate: accessEndDate);
        _dbContext.AddEmployee(employee);
        return employee;
    }

    [Fact]
    public async Task Handle_DeactivatesAnOwnedEmployee_OnceTheLastDayInTheSystemZoneHasPassed()
    {
        // Arrange
        var ended = Owned("A-1", SystemToday.PlusDays(-1));
        var lastDayToday = Owned("A-2", SystemToday);

        // Act
        var result = await CreateHandler().Handle(new DeactivateEmployeesPastAccessEndCommand(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
        ended.IsActive.Should().BeFalse();
        ended.DomainEvents.OfType<EmployeeDeactivatedEvent>().Should().ContainSingle();
        lastDayToday.IsActive.Should().BeTrue("the UTC day has moved on, but the system zone's has not");
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_LeavesManagedEmployeesAndOwnedOnesWithoutADate_Alone()
    {
        // Arrange
        var undated = Owned("A-3", null);
        var managed = Employee.CreateManaged(
            Connector.Workday, "acme", new PersonName("Managed", null, "M-1"), "M-1", null,
            new EmailAddress("m-1@acme.example"), null, null, null, null, true, null, EventActor.System, Now);
        _dbContext.AddEmployee(managed);

        // Act
        var result = await CreateHandler().Handle(new DeactivateEmployeesPastAccessEndCommand(), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().Be(0);
        undated.IsActive.Should().BeTrue();
        managed.IsActive.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_IgnoresAnAlreadyInactiveEmployee()
    {
        // Arrange
        Owned("A-4", SystemToday.PlusDays(-30), isActive: false);

        // Act
        var result = await CreateHandler().Handle(new DeactivateEmployeesPastAccessEndCommand(), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().Be(0);
    }
}
