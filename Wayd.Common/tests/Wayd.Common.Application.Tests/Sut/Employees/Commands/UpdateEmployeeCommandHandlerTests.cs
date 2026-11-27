using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Employees.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Identity;
using Wayd.Common.Models;
using Wayd.Tests.Shared;

namespace Wayd.Common.Application.Tests.Sut.Employees.Commands;

public sealed class UpdateEmployeeCommandHandlerTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 6, 2, 0, 0);
    private static readonly LocalDate Today = new(2026, 6, 1);

    private readonly FakeWaydDbContext _dbContext = new();

    private UpdateEmployeeCommandHandler CreateHandler()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns(SystemUser.Id);
        var calendar = new Mock<ISystemCalendar>();
        calendar.Setup(c => c.Today(It.IsAny<CancellationToken>())).ReturnsAsync(Today);

        return new UpdateEmployeeCommandHandler(
            _dbContext,
            new TestingDateTimeProvider(new FakeClock(Now)),
            calendar.Object,
            currentUser.Object,
            NullLogger<UpdateEmployeeCommandHandler>.Instance);
    }

    private static UpdateEmployeeCommand Command(Employee employee, LocalDate accessEndDate, string? jobTitle = "Advisor to the CTO") =>
        new(employee.Id, employee.Name, employee.EmployeeNumber, employee.HireDate, employee.Email, jobTitle,
            employee.Department, employee.OfficeLocation, employee.ManagerId, "Advisor", accessEndDate);

    private Employee Owned(LocalDate? accessEndDate, bool isActive = true)
    {
        var employee = Employee.Create(
            new PersonName("Riley", null, "Moss"), "ADV-1", null, new EmailAddress("riley@acme.example"),
            null, null, null, null, isActive, null, EventActor.System, Now, accessEndDate: accessEndDate);
        _dbContext.AddEmployee(employee);
        return employee;
    }

    [Fact]
    public async Task Handle_OwnedEmployee_SavesTheAccessEndDateAndType()
    {
        // Arrange
        var employee = Owned(new LocalDate(2026, 12, 31));

        // Act
        var result = await CreateHandler().Handle(Command(employee, new LocalDate(2027, 6, 30)), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        employee.AccessEndDate.Should().Be(new LocalDate(2027, 6, 30));
        employee.EmployeeType.Should().Be("Advisor");
        employee.JobTitle.Should().Be("Advisor to the CTO");
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ManagedEmployee_IsRefusedAndUnchanged()
    {
        // Arrange
        var employee = Employee.CreateManaged(
            Connector.Workday, "acme", new PersonName("Avery", null, "Chen"), "E-1", null, new EmailAddress("avery@acme.example"),
            "Engineer", null, null, null, true, null, EventActor.System, Now);
        _dbContext.AddEmployee(employee);

        // Act
        var result = await CreateHandler().Handle(Command(employee, new LocalDate(2027, 6, 30)), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        employee.JobTitle.Should().Be("Engineer");
        employee.AccessEndDate.Should().BeNull();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_PastAccessEndDate_IsAccepted()
    {
        // Arrange — ending access early is the admin's call; the access check does the rest
        var employee = Owned(new LocalDate(2026, 12, 31));

        // Act
        var result = await CreateHandler().Handle(Command(employee, Today.PlusDays(-1)), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        employee.AccessEndDate.Should().Be(Today.PlusDays(-1));
        employee.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ExtendingAnEndedAccess_ReactivatesTheEmployee()
    {
        // Arrange — deactivated by the access check after its last day
        var employee = Owned(Today.PlusDays(-5), isActive: false);

        // Act
        var result = await CreateHandler().Handle(Command(employee, Today.PlusDays(90)), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        employee.IsActive.Should().BeTrue();
    }
}
