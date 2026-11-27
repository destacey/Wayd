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
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Organization;
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

        var command = SyncCommand(
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

        var command = SyncCommand(
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

        var command = SyncCommand(
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

        var command = SyncCommand(
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

        var command = SyncCommand(
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

        var command = SyncCommand(
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

        var command = SyncCommand(
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
    [InlineData(64, "EmployeeNumber")]
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

        var command = SyncCommand(
            [record],
            EmployeeMatchProperty.Email,
            deactivateMissing: false);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        (await GetEmployees()).Should().HaveCount(1, $"a {field} of exactly {maxLength} characters fits the column");
    }

    [Fact]
    public async Task Handle_NewEmployee_PersistsEveryReportedWorkEmail()
    {
        // Arrange
        var record = FakeExternalEmployee("E-5001", "avery.chen@acme.example", isActive: true) with
        {
            Emails =
            [
                new ExternalEmployeeEmail(new EmailAddress("avery.chen@acme.example"), IsPrimary: true),
                new ExternalEmployeeEmail(new EmailAddress("avery.chen@acme-legacy.example"), IsPrimary: false),
            ],
        };
        var command = SyncCommand([record], EmployeeMatchProperty.EmployeeNumber);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var employee = (await GetEmployees()).Single();
        employee.Emails.Select(e => e.Email.Value).Should().BeEquivalentTo(
            ["avery.chen@acme.example", "avery.chen@acme-legacy.example"]);
        employee.Emails.Single(e => e.IsPrimary).Email.Value.Should().Be("avery.chen@acme.example");
    }

    [Fact]
    public async Task Handle_ExistingEmployee_RemovesEmailsTheSourceStoppedReporting()
    {
        // Arrange — the source is the system of record, so a dropped address is dropped here too.
        var existing = CreateExistingEmployee("E-5002", "jordan.blake@acme.example");
        existing.SyncEmails(
        [
            (new EmailAddress("jordan.blake@acme.example"), true),
            (new EmailAddress("jordan.blake@acme-legacy.example"), false),
        ], EventActor.System, _dateTimeProvider.Now);
        Seed(existing);

        var record = FakeExternalEmployee("E-5002", "jordan.blake@acme.example", isActive: true) with
        {
            Emails = [new ExternalEmployeeEmail(new EmailAddress("jordan.blake@acme.example"), IsPrimary: true)],
        };
        var command = SyncCommand([record], EmployeeMatchProperty.EmployeeNumber);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var employee = (await GetEmployees()).Single();
        employee.Emails.Select(e => e.Email.Value).Should().BeEquivalentTo(["jordan.blake@acme.example"]);
    }

    [Fact]
    public async Task Handle_EmailChanged_MovesPrimaryAndRetainsTheFormerAddress()
    {
        // Arrange — the tenant-migration shape, which is what makes the old address recoverable.
        var existing = CreateExistingEmployee("E-5003", "sam.ortiz@acme-legacy.example");
        Seed(existing);

        var record = FakeExternalEmployee("E-5003", "sam.ortiz@acme.example", isActive: true) with
        {
            Emails =
            [
                new ExternalEmployeeEmail(new EmailAddress("sam.ortiz@acme.example"), IsPrimary: true),
                new ExternalEmployeeEmail(new EmailAddress("sam.ortiz@acme-legacy.example"), IsPrimary: false),
            ],
        };
        var command = SyncCommand([record], EmployeeMatchProperty.EmployeeNumber);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var employee = (await GetEmployees()).Single();
        employee.Email.Value.Should().Be("sam.ortiz@acme.example");
        employee.Emails.Single(e => e.IsPrimary).Email.Value.Should().Be("sam.ortiz@acme.example");
        employee.Emails.Select(e => e.Email.Value).Should().Contain("sam.ortiz@acme-legacy.example");
    }

    [Fact]
    public async Task Handle_SourceReportsNoEmails_StillRecordsTheCanonicalAddress()
    {
        // Arrange — connectors that expose a single address leave Emails empty.
        var record = FakeExternalEmployee("E-5004", "solo@acme.example", isActive: true);
        var command = SyncCommand([record], EmployeeMatchProperty.EmployeeNumber);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var employee = (await GetEmployees()).Single();
        employee.Emails.Should().ContainSingle();
        employee.Emails.Single().Email.Value.Should().Be("solo@acme.example");
        employee.Emails.Single().IsPrimary.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_AllRecordsValid_ReportsThemAsUpsertedWithNoSkips()
    {
        // Arrange
        var payload = new IExternalEmployee[]
        {
            FakeExternalEmployee("E-6001", "one@acme.example", isActive: true),
            FakeExternalEmployee("E-6002", "two@acme.example", isActive: true),
        };
        var command = SyncCommand(payload, EmployeeMatchProperty.EmployeeNumber);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Value.Upserted.Should().Be(2);
        result.Value.Skipped.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_OverLongField_ReportsTheRecordAsSkippedRatherThanUpserted()
    {
        // Arrange — the run still succeeds, so without the reported count an admin would see this
        // as a clean sync.
        var payload = new IExternalEmployee[]
        {
            FakeExternalEmployee("E-6003", "fine@acme.example", isActive: true),
            FakeExternalEmployee("E-6004", "fine2@acme.example", isActive: true) with
            {
                JobTitle = new string('x', 257),
            },
        };
        var command = SyncCommand(payload, EmployeeMatchProperty.EmployeeNumber);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Upserted.Should().Be(1);
        result.Value.Skipped.Should().ContainKey("E-6004");
    }

    [Fact]
    public async Task Handle_MatchByNumber_KeysOnDifferentRows_UpdatesTheNumberRowAndLeavesTheEmailUnapplied()
    {
        // Arrange — EmployeeNumber resolves to one row, email to a different one.
        var byNumber = CreateExistingEmployee("E-6005", "byNumber@acme.example");
        var byEmail = CreateExistingEmployee("E-6006", "byEmail@acme.example");
        Seed(byNumber, byEmail);

        var record = FakeExternalEmployee("E-6005", "byEmail@acme.example", isActive: true) with
        {
            JobTitle = "Principal Engineer",
            Emails =
            [
                new ExternalEmployeeEmail(new EmailAddress("byEmail@acme.example"), IsPrimary: true),
                new ExternalEmployeeEmail(new EmailAddress("alias@acme.example"), IsPrimary: false),
            ],
        };
        var command = SyncCommand([record], EmployeeMatchProperty.EmployeeNumber);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Upserted.Should().Be(1);
        result.Value.Skipped.Should().BeEmpty();
        result.Value.Conflicts.Should().ContainKey("E-6005");
        result.Value.Conflicts["E-6005"].Should()
            .Contain("Email 'byEmail@acme.example' was not applied")
            .And.Contain($"to employee {byNumber.Key} (Existing E-6005, E-6005)")
            .And.Contain($"because employee {byEmail.Key} (Existing E-6006, E-6006)");

        byNumber.IsActive.Should().BeTrue("the matched row is claimed, so the deactivation pass leaves it alone");
        byNumber.JobTitle.Should().Be("Principal Engineer");
        byNumber.Email.Value.Should().Be("byNumber@acme.example");
        byNumber.Emails.Select(e => e.Email.Value).Should().BeEquivalentTo(["byNumber@acme.example", "alias@acme.example"]);

        byEmail.Email.Value.Should().Be("byEmail@acme.example");
        byEmail.EmployeeNumber.Should().Be("E-6006");
        byEmail.JobTitle.Should().Be("Engineer");
        byEmail.IsActive.Should().BeFalse("the record did not claim the row holding the other key");
    }

    [Fact]
    public async Task Handle_MatchByEmail_KeysOnDifferentRows_UpdatesTheEmailRowAndLeavesTheNumberUnapplied()
    {
        // Arrange — EmployeeNumber resolves to one row, email to a different one.
        var byNumber = CreateExistingEmployee("E-6010", "byNumber@acme.example");
        var byEmail = CreateExistingEmployee("E-6011", "byEmail@acme.example");
        Seed(byNumber, byEmail);

        var record = FakeExternalEmployee("E-6010", "byEmail@acme.example", isActive: true) with
        {
            JobTitle = "Principal Engineer",
        };
        var command = SyncCommand([record], EmployeeMatchProperty.Email);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Upserted.Should().Be(1);
        result.Value.Skipped.Should().BeEmpty();
        result.Value.Conflicts.Should().ContainKey("E-6010");
        result.Value.Conflicts["E-6010"].Should()
            .Contain("EmployeeNumber 'E-6010' was not applied")
            .And.Contain($"to employee {byEmail.Key} (Existing E-6011, E-6011)")
            .And.Contain($"because employee {byNumber.Key} (Existing E-6010, E-6010)");

        byEmail.IsActive.Should().BeTrue("the matched row is claimed, so the deactivation pass leaves it alone");
        byEmail.JobTitle.Should().Be("Principal Engineer");
        byEmail.EmployeeNumber.Should().Be("E-6011");
        byEmail.Emails.Select(e => e.Email.Value).Should().BeEquivalentTo(["byEmail@acme.example"]);

        byNumber.EmployeeNumber.Should().Be("E-6010");
        byNumber.Email.Value.Should().Be("byNumber@acme.example");
        byNumber.JobTitle.Should().Be("Engineer");
        byNumber.IsActive.Should().BeFalse("the record did not claim the row holding the other key");
    }

    [Fact]
    public async Task Handle_TwoRecordsResolveToOneRow_WithUnresolvedManagers_AppliesBoth()
    {
        // Arrange — record one matches row A by email and keeps A's number (B holds the payload's);
        // record two carries A's number, so both records resolve to row A.
        var rowA = CreateExistingEmployee("E-6014", "a@acme.example");
        var rowB = CreateExistingEmployee("E-6015", "b@acme.example");
        Seed(rowA, rowB);

        var first = FakeExternalEmployee("E-6015", "a@acme.example", isActive: true) with { ManagerEmployeeNumber = "M-404" };
        var second = FakeExternalEmployee("E-6014", "a2@acme.example", isActive: true) with
        {
            JobTitle = "Staff Engineer",
            ManagerEmployeeNumber = "M-405",
        };
        var command = SyncCommand([first, second], EmployeeMatchProperty.Email, deactivateMissing: false);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Skipped.Should().BeEmpty();
        rowA.JobTitle.Should().Be("Staff Engineer");
    }

    [Fact]
    public async Task Handle_PaddedEmployeeNumber_MatchesTheStoredNumber()
    {
        // Arrange
        var existing = CreateExistingEmployee("E-6016", "padded@acme.example");
        Seed(existing);

        var record = FakeExternalEmployee(" E-6016 ", "renamed@acme.example", isActive: true);
        var command = SyncCommand([record], EmployeeMatchProperty.EmployeeNumber);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        (await GetEmployees()).Should().ContainSingle();
        existing.Email.Value.Should().Be("renamed@acme.example");
    }

    [Fact]
    public async Task Handle_ManagerNumberVacatedEarlierInTheRun_DoesNotResolveToTheRowThatLeftIt()
    {
        // Arrange — the first record moves the row off M-OLD; the second names M-OLD as its manager.
        var manager = CreateExistingEmployee("M-OLD", "manager@acme.example");
        var report = CreateExistingEmployee("E-6017", "report@acme.example");
        Seed(manager, report);

        var movesNumber = FakeExternalEmployee("M-NEW", "manager@acme.example", isActive: true);
        var namesOldNumber = FakeExternalEmployee("E-6017", "report@acme.example", isActive: true) with { ManagerEmployeeNumber = "M-OLD" };
        var command = SyncCommand([movesNumber, namesOldNumber], EmployeeMatchProperty.Email, deactivateMissing: false);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        manager.EmployeeNumber.Should().Be("M-NEW");
        report.ManagerId.Should().BeNull("no employee holds M-OLD any more");
    }

    [Fact]
    public async Task Handle_ManagerNumberTakenEarlierInTheRun_ResolvesToTheRowNowHoldingIt()
    {
        // Arrange
        var manager = CreateExistingEmployee("M-OLD2", "manager2@acme.example");
        var report = CreateExistingEmployee("E-6018", "report2@acme.example");
        Seed(manager, report);

        var movesNumber = FakeExternalEmployee("M-NEW2", "manager2@acme.example", isActive: true);
        var namesNewNumber = FakeExternalEmployee("E-6018", "report2@acme.example", isActive: true) with { ManagerEmployeeNumber = "M-NEW2" };
        var command = SyncCommand([movesNumber, namesNewNumber], EmployeeMatchProperty.Email, deactivateMissing: false);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        report.ManagerId.Should().Be(manager.Id);
    }

    [Fact]
    public async Task Handle_MatchByEmail_KeyConflictWithMissingManager_ResolvesTheManagerOnTheMatchedRow()
    {
        // Arrange — the payload's number belongs to another row, so the manager fix-up must not find that one.
        var byNumber = CreateExistingEmployee("E-6012", "byNumber@acme.example");
        var byEmail = CreateExistingEmployee("E-6013", "byEmail@acme.example");
        Seed(byNumber, byEmail);

        var record = FakeExternalEmployee("E-6012", "byEmail@acme.example", isActive: true) with
        {
            ManagerEmployeeNumber = "M-1",
        };
        var manager = FakeExternalEmployee("M-1", "manager@acme.example", isActive: true);
        var command = SyncCommand([record, manager], EmployeeMatchProperty.Email, deactivateMissing: false);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var managerRow = (await GetEmployees()).Single(e => e.EmployeeNumber == "M-1");
        byEmail.ManagerId.Should().Be(managerRow.Id);
        byNumber.ManagerId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_OverLongAdditionalEmail_ReportsTheRecordAsSkipped()
    {
        // Arrange — EmployeeEmails.Email has the same 256 limit, and an over-long entry would
        // truncate-and-roll-back the whole batch rather than just this record.
        var longAddress = new string('x', 250) + "@acme.example";
        var record = FakeExternalEmployee("E-6007", "ok@acme.example", isActive: true) with
        {
            Emails =
            [
                new ExternalEmployeeEmail(new EmailAddress("ok@acme.example"), IsPrimary: true),
                new ExternalEmployeeEmail(new EmailAddress(longAddress), IsPrimary: false),
            ],
        };
        var command = SyncCommand([record], EmployeeMatchProperty.EmployeeNumber);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Value.Skipped.Should().ContainKey("E-6007");
        (await GetEmployees()).Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenARecordThrows_SkipsItAndSavesTheRest()
    {
        // Arrange — a null work email throws in the pre-flight checks, before the record's update is applied.
        var existing = CreateExistingEmployee("E-6008", "casey.lane@acme.example");
        Seed(existing);
        var eventsBefore = existing.DomainEvents.Count;

        var refused = FakeExternalEmployee("E-6008", "casey.lane@acme.example", isActive: true) with
        {
            JobTitle = "Principal Engineer",
            Emails = [new ExternalEmployeeEmail(null!, IsPrimary: false)],
        };
        var accepted = FakeExternalEmployee("E-6009", "riley.moss@acme.example", isActive: true);
        var command = SyncCommand([refused, accepted], EmployeeMatchProperty.EmployeeNumber, deactivateMissing: false);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Skipped.Should().ContainKey("E-6008");
        existing.JobTitle.Should().Be("Engineer");
        existing.DomainEvents.Should().HaveCount(eventsBefore);
        (await GetEmployees()).Select(e => e.EmployeeNumber).Should().BeEquivalentTo(["E-6008", "E-6009"]);
        _dbContext.SaveChangesCallCount.Should().Be(1);
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

    [Fact]
    public async Task Handle_FullSync_LeavesAnOwnedEmployeeItDidNotSendActive()
    {
        // Arrange
        var owned = CreateOwnedEmployee("ADV-1", "advisor@acme.example");
        Seed(owned);
        var command = SyncCommand([FakeExternalEmployee("E-9101", "someone@acme.example", isActive: true)], EmployeeMatchProperty.EmployeeNumber);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        owned.IsActive.Should().BeTrue("the sync deactivates only employees its own system manages");
        owned.IsManaged.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_FullSync_DeactivatesAPreviousSystemsEmployeeItDidNotSend()
    {
        // Arrange — the organization moved from Entra; this person is not in the new source.
        var otherSystems = Employee.CreateManaged(
            Connector.Entra, "other-tenant", new PersonName("Other", null, "System"), "0b7c", null,
            new EmailAddress("other@acme.example"), null, null, null, null, true, null, EventActor.System, _dateTimeProvider.Now);
        Seed(otherSystems);
        var command = SyncCommand([FakeExternalEmployee("E-9102", "someone@acme.example", isActive: true)], EmployeeMatchProperty.EmployeeNumber);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        otherSystems.IsActive.Should().BeFalse("the one active people connection is the source of truth for every synced employee");
        otherSystems.IsManagedBy(Connector.Entra, "other-tenant").Should().BeTrue("it is taken over only if the new source sends it");
    }

    [Fact]
    public async Task Handle_PreviousSystemsInactiveEmployeeSentByTheNewSource_TakesItOverAndReactivatesIt()
    {
        // Arrange
        var leftBehind = Employee.CreateManaged(
            Connector.Entra, "other-tenant", new PersonName("Left", null, "Behind"), "E-9107", null,
            new EmailAddress("left@acme.example"), null, null, null, null, false, null, EventActor.System, _dateTimeProvider.Now);
        Seed(leftBehind);
        var command = SyncCommand([FakeExternalEmployee("E-9107", "left@acme.example", isActive: true)], EmployeeMatchProperty.EmployeeNumber);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        leftBehind.IsManagedBy(SyncConnector, SyncSystemId).Should().BeTrue();
        leftBehind.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NewRecord_CreatesAnEmployeeManagedByTheSyncsSystem()
    {
        // Arrange
        var command = SyncCommand([FakeExternalEmployee("E-9103", "new@acme.example", isActive: true)], EmployeeMatchProperty.Email);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var created = (await GetEmployees()).Single();
        created.IsManagedBy(SyncConnector, SyncSystemId).Should().BeTrue();
        created.OwnershipInfo.ExternalId.Should().Be("E-9103");
    }

    [Theory]
    [InlineData(EmployeeMatchProperty.EmployeeNumber)]
    [InlineData(EmployeeMatchProperty.Email)]
    public async Task Handle_OwnedEmployeeOnTheConfiguredKey_AdoptsAndUpdatesIt(EmployeeMatchProperty matchBy)
    {
        // Arrange — the record matches the owned employee on the configured key only.
        var owned = CreateOwnedEmployee("E-9104", "adopt@acme.example");
        Seed(owned);
        var record = matchBy == EmployeeMatchProperty.EmployeeNumber
            ? FakeExternalEmployee("E-9104", "adopt.new@acme.example", isActive: true)
            : FakeExternalEmployee("E-9199", "adopt@acme.example", isActive: true);
        var command = SyncCommand([record with { JobTitle = "Principal Engineer" }], matchBy);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Skipped.Should().BeEmpty();
        (await GetEmployees()).Should().ContainSingle("adoption keeps the employee rather than creating another");
        owned.IsManagedBy(SyncConnector, SyncSystemId).Should().BeTrue();
        owned.JobTitle.Should().Be("Principal Engineer");
        owned.OwnershipInfo.ExternalId.Should().Be(owned.EmployeeNumber);
        owned.DomainEvents.OfType<EmployeeAdoptedEvent>().Should().ContainSingle();
    }

    [Theory]
    [InlineData(EmployeeMatchProperty.EmployeeNumber)]
    [InlineData(EmployeeMatchProperty.Email)]
    public async Task Handle_OwnedEmployeeOnTheOtherKeyOnly_SkipsAndReportsTheRecord(EmployeeMatchProperty matchBy)
    {
        // Arrange — the record matches the owned employee only on the key the connection does not match by.
        var owned = CreateOwnedEmployee("E-9105", "other-key@acme.example");
        Seed(owned);
        var record = matchBy == EmployeeMatchProperty.EmployeeNumber
            ? FakeExternalEmployee("E-9198", "other-key@acme.example", isActive: true)
            : FakeExternalEmployee("E-9105", "other-key.new@acme.example", isActive: true);
        var command = SyncCommand([record with { JobTitle = "Principal Engineer" }], matchBy);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Skipped.Should().ContainKey(record.EmployeeNumber)
            .WhoseValue.Should().Contain("Wayd owns").And.Contain("Match Employees By");
        result.Value.Upserted.Should().Be(0);
        (await GetEmployees()).Should().ContainSingle();
        owned.IsManaged.Should().BeFalse();
        owned.JobTitle.Should().Be("Engineer");
        owned.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_AnotherSystemsEmployeeOnTheConfiguredKey_TakesItOver()
    {
        // Arrange — the organization moved from Entra to this sync's system.
        var fromEntra = Employee.CreateManaged(
            Connector.Entra, "old-tenant", new PersonName("Avery", null, "Chen"), "E-9106", null,
            new EmailAddress("avery@acme.example"), null, null, null, null, true, null, EventActor.System, _dateTimeProvider.Now);
        Seed(fromEntra);
        var command = SyncCommand([FakeExternalEmployee("E-9106", "avery@acme.example", isActive: true)], EmployeeMatchProperty.EmployeeNumber);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Upserted.Should().Be(1);
        fromEntra.IsManagedBy(SyncConnector, SyncSystemId).Should().BeTrue();
        fromEntra.DomainEvents.OfType<EmployeeTakenOverEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_OwnedEmployeeWithANumberTooLongToReference_SkipsTheRecordAndSavesTheRest()
    {
        // Arrange — only an employee that predates the 64-character limit has one. Matched by email, and the
        // payload's number is another employee's, so the conflict would keep the long number as the external id.
        var longNumber = new string('L', Employee.ExternalReferenceMaxLength + 1);
        var legacy = CreateOwnedEmployee(longNumber, "legacy@acme.example");
        var holder = CreateExistingEmployee("E-9110", "holder@acme.example");
        Seed(legacy, holder);
        var command = SyncCommand(
            [
                FakeExternalEmployee("E-9110", "legacy@acme.example", isActive: true),
                FakeExternalEmployee("E-9111", "fine@acme.example", isActive: true),
            ],
            EmployeeMatchProperty.Email,
            deactivateMissing: false);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Skipped.Should().ContainKey("E-9110").WhoseValue.Should().Contain("longer than");
        legacy.IsManaged.Should().BeFalse();
        (await GetEmployees()).Should().Contain(e => e.EmployeeNumber == "E-9111");
    }

    /// <summary>An employee Wayd owns, as an admin or an import creates one.</summary>
    private Employee CreateOwnedEmployee(string employeeNumber, string email) =>
        Employee.Create(
            new PersonName("Owned", null, employeeNumber), employeeNumber, null, new EmailAddress(email),
            "Engineer", "Engineering", "Remote", null, isActive: true, employeeType: "Advisor",
            EventActor.System, _dateTimeProvider.Now, accessEndDate: new LocalDate(2027, 12, 31));

    private const Connector SyncConnector = Connector.Workday;
    private const string SyncSystemId = "acme";

    private static BulkUpsertEmployeesCommand SyncCommand(IEnumerable<IExternalEmployee> payload, EmployeeMatchProperty matchBy, bool deactivateMissing = true) =>
        new(payload, SyncConnector, SyncSystemId, matchBy, deactivateMissing);

    /// <summary>An employee this test's people sync already manages.</summary>
    private Employee CreateExistingEmployee(string employeeNumber, string email) =>
        Employee.CreateManaged(
            SyncConnector,
            SyncSystemId,
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
            EventActor.System,
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
        public IReadOnlyList<ExternalEmployeeEmail> Emails { get; init; } = [];
    }
}
