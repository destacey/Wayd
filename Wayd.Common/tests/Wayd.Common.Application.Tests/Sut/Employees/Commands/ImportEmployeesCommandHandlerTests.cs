using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Employees.Commands;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Models;
using Wayd.Tests.Shared;

namespace Wayd.Common.Application.Tests.Sut.Employees.Commands;

public class ImportEmployeesCommandHandlerTests
{
    private readonly FakeWaydDbContext _dbContext = new();
    private readonly TestingDateTimeProvider _dateTimeProvider =
        new(new FakeClock(Instant.FromUtc(2026, 6, 2, 0, 0)));

    private ImportEmployeesCommandHandler CreateHandler() =>
        new(_dbContext, _dateTimeProvider, NullLogger<ImportEmployeesCommandHandler>.Instance);

    private static ImportEmployeeDto Row(string employeeNumber, string email, string? managerNumber = null, bool isActive = true, string? employeeType = null) =>
        new(
            employeeNumber,
            FirstName: "Test",
            MiddleName: null,
            LastName: employeeNumber,
            Email: new EmailAddress(email),
            HireDate: null,
            JobTitle: "Engineer",
            Department: "Engineering",
            OfficeLocation: "Remote",
            ManagerNumber: managerNumber,
            IsActive: isActive,
            EmployeeType: employeeType);

    private async Task<List<Employee>> GetEmployees() =>
        await _dbContext.Employees.ToListAsync(TestContext.Current.CancellationToken);

    [Fact]
    public async Task Handle_ImportsEmployeeType()
    {
        // Arrange — regression guard: the handler must thread EmployeeType through, not drop it as null.
        var command = new ImportEmployeesCommand(
        [
            Row("E-1001", "regular@acme.example", employeeType: "Employee"),
            Row("E-1002", "contractor@acme.example", employeeType: "Contractor"),
            Row("E-1003", "unspecified@acme.example"),
        ]);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var byNumber = (await GetEmployees()).ToDictionary(e => e.EmployeeNumber);
        byNumber["E-1001"].EmployeeType.Should().Be("Employee");
        byNumber["E-1002"].EmployeeType.Should().Be("Contractor");
        byNumber["E-1003"].EmployeeType.Should().BeNull();
    }

    [Fact]
    public async Task Handle_CreatesAllEmployees()
    {
        // Arrange
        var command = new ImportEmployeesCommand(
        [
            Row("E-1001", "ada@acme.example"),
            Row("E-1002", "grace@acme.example"),
            Row("E-1003", "alan@acme.example"),
        ]);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var employees = await GetEmployees();
        employees.Should().HaveCount(3);
        employees.Should().OnlyContain(e => e.IsActive);
    }

    [Fact]
    public async Task Handle_LinksManager_WhenManagerAppearsLaterInBatch()
    {
        // Arrange — a report references its manager, and the manager row comes AFTER the report in the batch.
        var command = new ImportEmployeesCommand(
        [
            Row("E-1002", "report@acme.example", managerNumber: "E-1001"),
            Row("E-1001", "boss@acme.example"),
        ]);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var byNumber = (await GetEmployees()).ToDictionary(e => e.EmployeeNumber);
        var boss = byNumber["E-1001"];
        byNumber["E-1002"].ManagerId.Should().Be(boss.Id);
        boss.ManagerId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_LinksManager_WhenManagerAlreadyExists()
    {
        // Arrange — the manager is already in the database (not part of this batch).
        var existingManager = Employee.Create(
            new PersonName("Existing", null, "Boss"),
            "E-9000",
            hireDate: null,
            new EmailAddress("existing.boss@acme.example"),
            jobTitle: "Director",
            department: "Engineering",
            officeLocation: "Remote",
            managerId: null,
            isActive: true,
            employeeType: "Employee",
            _dateTimeProvider.Now);
        _dbContext.Employees.Add(existingManager);

        var command = new ImportEmployeesCommand(
        [
            Row("E-1002", "report@acme.example", managerNumber: "E-9000"),
        ]);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var report = (await GetEmployees()).Single(e => e.EmployeeNumber == "E-1002");
        report.ManagerId.Should().Be(existingManager.Id);
    }

    [Fact]
    public async Task Handle_ImportsWithoutManager_WhenManagerNumberUnresolved()
    {
        // Arrange — references a manager number that is nowhere to be found.
        var command = new ImportEmployeesCommand(
        [
            Row("E-1002", "report@acme.example", managerNumber: "E-DOES-NOT-EXIST"),
        ]);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var report = (await GetEmployees()).Single();
        report.ManagerId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ImportsFormerEmployee_AsInactive()
    {
        // Arrange — a former employee (migration / historical fixture), plus a current one.
        var command = new ImportEmployeesCommand(
        [
            Row("E-1001", "current@acme.example"),
            Row("E-1002", "former@acme.example", isActive: false),
        ]);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert — created then deactivated through the domain.
        result.IsSuccess.Should().BeTrue();
        var byNumber = (await GetEmployees()).ToDictionary(e => e.EmployeeNumber);
        byNumber["E-1001"].IsActive.Should().BeTrue();
        byNumber["E-1002"].IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_LinksManager_EvenWhenThatManagerIsImportedInactive()
    {
        // Arrange — a since-departed manager can still be recorded as a report's manager.
        var command = new ImportEmployeesCommand(
        [
            Row("E-1001", "former.boss@acme.example", isActive: false),
            Row("E-1002", "report@acme.example", managerNumber: "E-1001"),
        ]);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var byNumber = (await GetEmployees()).ToDictionary(e => e.EmployeeNumber);
        byNumber["E-1002"].ManagerId.Should().Be(byNumber["E-1001"].Id);
        byNumber["E-1001"].IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_DoesNotDeactivateExistingEmployees()
    {
        // Arrange — an existing active employee absent from the import must remain untouched (additive import).
        var existing = Employee.Create(
            new PersonName("Keep", null, "Active"),
            "E-8000",
            hireDate: null,
            new EmailAddress("keep.active@acme.example"),
            jobTitle: "Engineer",
            department: "Engineering",
            officeLocation: "Remote",
            managerId: null,
            isActive: true,
            employeeType: "Employee",
            _dateTimeProvider.Now);
        _dbContext.Employees.Add(existing);

        var command = new ImportEmployeesCommand([Row("E-1001", "new@acme.example")]);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var byNumber = (await GetEmployees()).ToDictionary(e => e.EmployeeNumber);
        byNumber["E-8000"].IsActive.Should().BeTrue();
        byNumber.Should().ContainKey("E-1001");
    }
}
