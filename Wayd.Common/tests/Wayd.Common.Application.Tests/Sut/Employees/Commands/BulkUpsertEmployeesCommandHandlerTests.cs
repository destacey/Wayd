using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Employees.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Models;
using Wayd.Tests.Shared;

namespace Wayd.Common.Application.Tests.Sut.Employees.Commands;

public class BulkUpsertEmployeesCommandHandlerTests
{
    private readonly FakeWaydDbContext _dbContext = new();
    private readonly TestingDateTimeProvider _dateTimeProvider =
        new(new FakeClock(Instant.FromUtc(2026, 6, 2, 0, 0)));

    private BulkUpsertEmployeesCommandHandler CreateHandler() =>
        new(_dbContext, _dateTimeProvider, NullLogger<BulkUpsertEmployeesCommandHandler>.Instance);

    /// <summary>
    /// Regression test for the first-sync deactivation bug: on an empty database, every employee in
    /// the payload is newly created. With <c>DeactivateMissing = true</c> (the default for a full
    /// sync), the deactivation pass must NOT deactivate the rows this very sync just created.
    /// </summary>
    [Fact]
    public async Task Handle_FirstSync_NewActiveEmployeesRemainActive()
    {
        // Arrange — empty DB, payload of active employees, full sync (deactivateMissing: true).
        var payload = new IExternalEmployee[]
        {
            FakeExternalEmployee("E-1001", "ada@acme.example", isActive: true),
            FakeExternalEmployee("E-1002", "grace@acme.example", isActive: true),
            FakeExternalEmployee("E-1003", "alan@acme.example", isActive: true),
        };

        var command = new BulkUpsertEmployeesCommand(
            payload,
            EmployeeMatchProperty.EmployeeNumber,
            deactivateMissing: true);

        // Act
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var employees = await GetEmployees();
        employees.Should().HaveCount(3, "all three payload records should have been created");
        employees.Should().OnlyContain(e => e.IsActive,
            "employees created during the first sync are present in the payload and must stay active");
    }

    /// <summary>
    /// The deactivation pass should still deactivate employees that exist in the DB but are absent
    /// from the payload — while leaving both matched-existing and newly-created payload employees active.
    /// </summary>
    [Fact]
    public async Task Handle_DeactivatesOnlyEmployeesMissingFromPayload()
    {
        // Arrange — one existing active employee in the payload, one existing active employee NOT in
        // the payload, plus a brand-new employee in the payload.
        var existingMatched = CreateExistingEmployee("E-2001", "matched@acme.example");
        var existingMissing = CreateExistingEmployee("E-2002", "missing@acme.example");
        Seed(existingMatched, existingMissing);

        var payload = new IExternalEmployee[]
        {
            FakeExternalEmployee("E-2001", "matched@acme.example", isActive: true),
            FakeExternalEmployee("E-2003", "new@acme.example", isActive: true),
        };

        var command = new BulkUpsertEmployeesCommand(
            payload,
            EmployeeMatchProperty.EmployeeNumber,
            deactivateMissing: true);

        // Act
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var byNumber = (await GetEmployees()).ToDictionary(e => e.EmployeeNumber);

        byNumber["E-2001"].IsActive.Should().BeTrue("matched existing employee is in the payload");
        byNumber["E-2003"].IsActive.Should().BeTrue("new employee is in the payload");
        byNumber["E-2002"].IsActive.Should().BeFalse("existing employee absent from the payload is deactivated");
    }

    private void Seed(params Employee[] employees)
    {
        foreach (var employee in employees)
        {
            _dbContext.Employees.Add(employee);
        }
    }

    private async Task<List<Employee>> GetEmployees() =>
        await _dbContext.Employees.ToListAsync(CancellationToken.None);

    private Employee CreateExistingEmployee(string employeeNumber, string email) =>
        Employee.Create(
            new PersonName("Existing", null, employeeNumber),
            employeeNumber,
            hireDate: null,
            new EmailAddress(email),
            jobTitle: "Engineer",
            department: "Engineering",
            officeLocation: "Remote",
            managerId: null,
            isActive: true,
            employeeType: "Employee",
            _dateTimeProvider.Now);

    private static FakeExternalEmployeeRecord FakeExternalEmployee(string employeeNumber, string email, bool isActive) =>
        new()
        {
            EmployeeNumber = employeeNumber,
            Name = new PersonName("Test", null, employeeNumber),
            HireDate = null,
            Email = new EmailAddress(email),
            JobTitle = "Engineer",
            Department = "Engineering",
            OfficeLocation = "Remote",
            ManagerEmployeeNumber = null,
            IsActive = isActive,
            EmployeeType = "Employee",
        };

    private sealed record FakeExternalEmployeeRecord : IExternalEmployee
    {
        public required string EmployeeNumber { get; init; }
        public required PersonName Name { get; init; }
        public required Instant? HireDate { get; init; }
        public required EmailAddress Email { get; init; }
        public required string? JobTitle { get; init; }
        public required string? Department { get; init; }
        public required string? OfficeLocation { get; init; }
        public required string? ManagerEmployeeNumber { get; init; }
        public required bool IsActive { get; init; }
        public required string? EmployeeType { get; init; }
    }
}
