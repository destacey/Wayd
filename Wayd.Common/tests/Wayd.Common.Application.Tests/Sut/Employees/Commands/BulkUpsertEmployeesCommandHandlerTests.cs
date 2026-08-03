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
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

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
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var byNumber = (await GetEmployees()).ToDictionary(e => e.EmployeeNumber);

        byNumber["E-2001"].IsActive.Should().BeTrue("matched existing employee is in the payload");
        byNumber["E-2003"].IsActive.Should().BeTrue("new employee is in the payload");
        byNumber["E-2002"].IsActive.Should().BeFalse("existing employee absent from the payload is deactivated");
    }

    /// <summary>
    /// Regression test for the duplicate-key sync failure: an upstream email domain migration makes
    /// the configured Email key miss, but the employee still exists under the same EmployeeNumber.
    /// The upsert must fall back to the number key and update in place rather than creating a second
    /// row — the create would violate the unique EmployeeNumber index and fail the whole batch.
    /// </summary>
    [Fact]
    public async Task Handle_MatchByEmail_WhenEmailChanged_FallsBackToEmployeeNumber()
    {
        // Arrange — existing row under the old email; payload carries the migrated address.
        Seed(CreateExistingEmployee("E-3001", "gia.bachmann@old.example"));

        var payload = new IExternalEmployee[]
        {
            FakeExternalEmployee("E-3001", "gia.bachmann@new.example", isActive: true),
        };

        var command = new BulkUpsertEmployeesCommand(
            payload,
            EmployeeMatchProperty.Email,
            deactivateMissing: false);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var employees = await GetEmployees();
        employees.Should().HaveCount(1, "the email miss should fall back to EmployeeNumber, not create a second row");
        employees[0].Email.Value.Should().Be("gia.bachmann@new.example", "the existing row should be updated to the new address");
    }

    /// <summary>
    /// The mirror case: the connection matches on EmployeeNumber, but the source reissued the
    /// number. Email still identifies the person, so the upsert must fall back to it.
    /// </summary>
    [Fact]
    public async Task Handle_MatchByEmployeeNumber_WhenNumberChanged_FallsBackToEmail()
    {
        // Arrange — existing row under the old number; payload carries a new number, same email.
        Seed(CreateExistingEmployee("E-4001", "stable@acme.example"));

        var payload = new IExternalEmployee[]
        {
            FakeExternalEmployee("E-4002", "stable@acme.example", isActive: true),
        };

        var command = new BulkUpsertEmployeesCommand(
            payload,
            EmployeeMatchProperty.EmployeeNumber,
            deactivateMissing: false);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var employees = await GetEmployees();
        employees.Should().HaveCount(1, "the number miss should fall back to Email, not create a second row");
        employees[0].EmployeeNumber.Should().Be("E-4002", "the existing row should be updated to the new number");
    }

    /// <summary>
    /// When the two candidate keys resolve to two different existing rows, identity is genuinely
    /// ambiguous. The record is skipped and both rows are left untouched — merging them would be a
    /// guess, and picking either one silently corrupts the other.
    /// </summary>
    [Fact]
    public async Task Handle_WhenKeysResolveToDifferentEmployees_SkipsRecordAndLeavesBothIntact()
    {
        // Arrange — payload record matches one row by number and a different row by email.
        Seed(
            CreateExistingEmployee("E-5001", "byNumber@acme.example"),
            CreateExistingEmployee("E-5002", "byEmail@acme.example"));

        var payload = new IExternalEmployee[]
        {
            FakeExternalEmployee("E-5001", "byEmail@acme.example", isActive: true),
        };

        var command = new BulkUpsertEmployeesCommand(
            payload,
            EmployeeMatchProperty.Email,
            deactivateMissing: false);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue("an ambiguous record is skipped, not a batch failure");

        var byNumber = (await GetEmployees()).ToDictionary(e => e.EmployeeNumber);
        byNumber.Should().HaveCount(2, "no row should be created for the ambiguous record");
        byNumber["E-5001"].Email.Value.Should().Be("byNumber@acme.example", "the number-matched row is left untouched");
        byNumber["E-5002"].Email.Value.Should().Be("byEmail@acme.example", "the email-matched row is left untouched");
    }

    /// <summary>
    /// Regression test for the stale-lookup-index bug. Employee A migrates onto an address, and a
    /// later record in the same payload is the same person under a reissued number. The indexes must
    /// reflect A's in-run mutation, otherwise the second record misses both keys, takes the create
    /// branch, and collides with A on the unique email index — failing the entire batch.
    /// </summary>
    [Fact]
    public async Task Handle_WhenPayloadRecordTargetsEmailClaimedEarlierInSameRun_DoesNotCreateDuplicate()
    {
        // Arrange — one existing row that the first payload record migrates to a new address.
        Seed(CreateExistingEmployee("E-6001", "old@acme.example"));

        var payload = new IExternalEmployee[]
        {
            FakeExternalEmployee("E-6001", "migrated@acme.example", isActive: true),
            FakeExternalEmployee("E-6002", "migrated@acme.example", isActive: true),
        };

        var command = new BulkUpsertEmployeesCommand(
            payload,
            EmployeeMatchProperty.Email,
            deactivateMissing: false);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var employees = await GetEmployees();
        employees.Should().HaveCount(1,
            "the second record must match the row the first one migrated, not insert a duplicate email");
    }

    /// <summary>
    /// A single over-long field must skip only its own record. Reaching SaveChanges with it would
    /// throw a truncation error and roll back the entire batch, losing every other employee.
    /// </summary>
    [Fact]
    public async Task Handle_WhenRecordExceedsColumnLength_SkipsOnlyThatRecord()
    {
        // Arrange — EmployeeType has a 128-char column; this record blows past it.
        var payload = new IExternalEmployee[]
        {
            FakeExternalEmployee("E-7001", "ok-before@acme.example", isActive: true),
            FakeExternalEmployee("E-7002", "too-long@acme.example", isActive: true)
                with { EmployeeType = new string('x', 129) },
            FakeExternalEmployee("E-7003", "ok-after@acme.example", isActive: true),
        };

        var command = new BulkUpsertEmployeesCommand(
            payload,
            EmployeeMatchProperty.Email,
            deactivateMissing: false);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue("one malformed record must not fail the run");

        var employees = await GetEmployees();
        employees.Should().HaveCount(2, "only the over-long record should be skipped");
        employees.Select(e => e.EmployeeNumber).Should().BeEquivalentTo(["E-7001", "E-7003"]);
    }

    /// <summary>
    /// Pins the handler's length constants to the values configured in <c>EmployeeConfig</c>. If a
    /// migration widens or narrows a column, this test fails and points at the constant to update —
    /// the Application layer cannot reference Infrastructure to read them directly.
    /// </summary>
    [Theory]
    [InlineData(256, "EmployeeNumber")]
    [InlineData(128, "EmployeeType")]
    [InlineData(100, "MiddleName")]
    public async Task Handle_AcceptsValuesAtTheConfiguredColumnLimit(int maxLength, string field)
    {
        // Arrange — a value exactly at the limit must be accepted, not skipped.
        var atLimit = new string('x', maxLength);
        var record = FakeExternalEmployee("E-8001", "at-limit@acme.example", isActive: true);

        record = field switch
        {
            "EmployeeNumber" => record with { EmployeeNumber = atLimit },
            "EmployeeType" => record with { EmployeeType = atLimit },
            _ => record with { Name = new PersonName("First", atLimit, "Last") },
        };

        var command = new BulkUpsertEmployeesCommand(
            [record],
            EmployeeMatchProperty.Email,
            deactivateMissing: false);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        (await GetEmployees()).Should().HaveCount(1, $"a {field} of exactly {maxLength} characters fits the column");
    }

    private void Seed(params Employee[] employees)
    {
        foreach (var employee in employees)
        {
            _dbContext.Employees.Add(employee);
        }
    }

    private async Task<List<Employee>> GetEmployees() =>
        await _dbContext.Employees.ToListAsync(TestContext.Current.CancellationToken);

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
