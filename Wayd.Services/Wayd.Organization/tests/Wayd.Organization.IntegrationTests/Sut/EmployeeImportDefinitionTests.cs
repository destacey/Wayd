using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Application.Employees.Imports;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Imports;
using Wayd.Common.Models;
using Wayd.Organization.IntegrationTests.Infrastructure;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="EmployeeImportDefinition"/> against a real SQL Server container.
/// </summary>
/// <remarks>
/// The passes lean on the provider in ways an in-memory fake cannot check: employees carry NodaTime values
/// (<c>HireDate</c>, the audit columns), the duplicate checks and the manager lookup run real <c>IN</c>
/// queries, <c>Email</c> is a value converter, and the unique indexes behind the row-level rejections are a
/// database constraint. This is the shape of bug that has passed green unit tests here before.
/// <para>
/// It also uses the real <see cref="ImportPayloadSerializer"/>, so the payload round-trip a queued run
/// depends on — a DTO carrying <c>Instant</c> and <c>EmailAddress</c> written now and read back by a worker
/// later — is exercised rather than assumed.
/// </para>
/// <para>
/// Passes mutate but do not save — that is the runner's job, one SaveChanges per chunk — so these tests
/// save between passes exactly as the runner does.
/// </para>
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class EmployeeImportDefinitionTests
{
    private const int CreatePass = 0;
    private const int LinkPass = 1;
    private const int DeactivatePass = 2;

    private readonly SqlServerDbContextFixture _fixture;

    public EmployeeImportDefinitionTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    private static EmployeeImportDefinition CreateDefinition(Wayd.Infrastructure.Persistence.Context.WaydDbContext context)
    {
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(d => d.Now).Returns(SqlServerDbContextFixture.FixedNow);

        return new EmployeeImportDefinition(context, dateTimeProvider.Object, new ImportPayloadSerializer());
    }

    private static ImportEmployeeDto Employee(
        string number,
        string firstName,
        string lastName,
        string email,
        string? managerNumber = null,
        bool isActive = true,
        string[]? additionalEmails = null) =>
        new(
            number,
            firstName,
            null,
            lastName,
            new EmailAddress(email),
            HireDate: SqlServerDbContextFixture.FixedNow,
            JobTitle: "Engineer",
            Department: "Engineering",
            OfficeLocation: null,
            ManagerNumber: managerNumber,
            IsActive: isActive,
            AdditionalEmails: additionalEmails is null ? null : [.. additionalEmails.Select(e => new EmailAddress(e))]);

    private static ImportProcessRow[] Rows(EmployeeImportDefinition definition, params ImportEmployeeDto[] employees) =>
        [.. employees.Select((e, i) => ImportProcessRow.Create($"r{i + 1}", i + 1, definition.SerializeRow(e)))];

    /// <summary>Runs a pass and saves, the way the runner does for each chunk.</summary>
    private static async Task<ImportPassResult> RunPass(
        Wayd.Infrastructure.Persistence.Context.WaydDbContext context,
        EmployeeImportDefinition definition,
        int passIndex,
        ImportProcessRow[] rows,
        CancellationToken cancellationToken)
    {
        var result = await definition.ExecutePass(
            Guid.CreateVersion7(), passIndex, rows, isFinalChunk: true, cancellationToken);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        await context.SaveChangesAsync(cancellationToken);

        return result.Value;
    }

    [Fact]
    public async Task LinkManagers_ResolvesAManagerFromAnyRowOrderAcrossTheFile()
    {
        // Arrange — the report appears before its manager, so linkage cannot depend on row order
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);

        await using var context = _fixture.CreateContext();
        var definition = CreateDefinition(context);
        var rows = Rows(definition,
            Employee("E-1001", "Ada", "Lovelace", "ada@acme.example", managerNumber: "E-2001"),
            Employee("E-2001", "Grace", "Hopper", "grace@acme.example"));

        // Act — each pass runs to completion before the next begins, which is what makes the link resolvable
        await RunPass(context, definition, CreatePass, rows, cancellationToken);
        await RunPass(context, definition, LinkPass, rows, cancellationToken);

        // Assert
        await using var assertContext = _fixture.CreateContext();
        var employees = await assertContext.Employees.ToListAsync(cancellationToken);
        employees.Should().HaveCount(2);

        var manager = employees.Single(e => e.EmployeeNumber == "E-2001");
        var report = employees.Single(e => e.EmployeeNumber == "E-1001");
        report.ManagerId.Should().Be(manager.Id);
        manager.ManagerId.Should().BeNull();
    }

    [Fact]
    public async Task LinkManagers_ResolvesAManagerCreatedByAnEarlierChunk()
    {
        // Arrange — the manager is created and saved by a separate chunk of the create pass, so the link
        // pass has to find it by query rather than from anything held in memory
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);

        await using var context = _fixture.CreateContext();
        var definition = CreateDefinition(context);
        var managerRows = Rows(definition, Employee("E-2002", "Grace", "Hopper", "grace2@acme.example"));
        var reportRows = Rows(definition, Employee("E-1002", "Ada", "Lovelace", "ada2@acme.example", managerNumber: "E-2002"));

        // Act
        await RunPass(context, definition, CreatePass, managerRows, cancellationToken);
        await RunPass(context, definition, CreatePass, reportRows, cancellationToken);
        await RunPass(context, definition, LinkPass, reportRows, cancellationToken);

        // Assert
        await using var assertContext = _fixture.CreateContext();
        var manager = await assertContext.Employees.SingleAsync(e => e.EmployeeNumber == "E-2002", cancellationToken);
        var report = await assertContext.Employees.SingleAsync(e => e.EmployeeNumber == "E-1002", cancellationToken);
        report.ManagerId.Should().Be(manager.Id);
    }

    [Fact]
    public async Task CreateEmployees_PersistsWorkEmailsAndReadsThemBack()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);

        await using var context = _fixture.CreateContext();
        var definition = CreateDefinition(context);
        var rows = Rows(definition,
            Employee("E-3001", "Avery", "Chen", "avery.chen@acme.example",
                additionalEmails: ["avery.chen@acme-legacy.example"]));

        // Act
        await RunPass(context, definition, CreatePass, rows, cancellationToken);

        // Assert
        await using var assertContext = _fixture.CreateContext();
        var employee = await assertContext.Employees
            .Include(e => e.Emails)
            .SingleAsync(e => e.EmployeeNumber == "E-3001", cancellationToken);

        employee.Emails.Select(e => e.Email.Value).Should().BeEquivalentTo(
            ["avery.chen@acme.example", "avery.chen@acme-legacy.example"]);
        employee.Emails.Single(e => e.IsPrimary).Email.Value.Should().Be("avery.chen@acme.example");
    }

    /// <summary>
    /// The unique index on <c>EmployeeEmails.Email</c> spans the whole table, so two people cannot claim the
    /// same address. The pass rejects the second row itself — this pins the constraint that backs that, so
    /// the check and the database cannot drift apart.
    /// </summary>
    [Fact]
    public async Task CreateEmployees_RejectsTheSecondRowClaimingAnAddressRatherThanFailingTheChunk()
    {
        // Arrange — the collision is on an additional address, not a primary one
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);

        await using var context = _fixture.CreateContext();
        var definition = CreateDefinition(context);
        var rows = Rows(definition,
            Employee("E-3002", "Jordan", "Blake", "jordan@acme.example",
                additionalEmails: ["shared@acme-legacy.example"]),
            Employee("E-3003", "Sam", "Ortiz", "sam@acme.example",
                additionalEmails: ["shared@acme-legacy.example"]));

        // Act
        var result = await RunPass(context, definition, CreatePass, rows, cancellationToken);

        // Assert — the good row still lands, where the old whole-batch path lost both
        result.Rows.Single(r => r.ImportId == "r1").Failed.Should().BeFalse();
        result.Rows.Single(r => r.ImportId == "r2").Failed.Should().BeTrue();

        await using var assertContext = _fixture.CreateContext();
        var employees = await assertContext.Employees.ToListAsync(cancellationToken);
        employees.Should().ContainSingle().Which.EmployeeNumber.Should().Be("E-3002");
    }

    [Fact]
    public async Task CreateEmployees_RejectsARowWhoseAddressBelongsToSomeoneAlreadyStored()
    {
        // Arrange — the collision is against a row from an earlier run, so only a query finds it
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);

        await using var context = _fixture.CreateContext();
        var definition = CreateDefinition(context);
        await RunPass(context, definition, CreatePass,
            Rows(definition, Employee("E-4001", "Riley", "Nakamura", "riley@acme.example")), cancellationToken);

        var laterRows = Rows(definition, Employee("E-4002", "Casey", "Yun", "riley@acme.example"));

        // Act
        var result = await RunPass(context, definition, CreatePass, laterRows, cancellationToken);

        // Assert
        result.Rows.Single().Failed.Should().BeTrue();
        result.Rows.Single().Error.Should().Contain("email address");

        await using var assertContext = _fixture.CreateContext();
        (await assertContext.Employees.CountAsync(cancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task DeactivateLeavers_DeactivatesAfterTheLinkPassSoALeaverCanStillBeAManager()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);

        await using var context = _fixture.CreateContext();
        var definition = CreateDefinition(context);
        var rows = Rows(definition,
            Employee("E-5001", "Morgan", "Diaz", "morgan@acme.example", isActive: false),
            Employee("E-5002", "Alex", "Park", "alex@acme.example", managerNumber: "E-5001"));

        // Act
        await RunPass(context, definition, CreatePass, rows, cancellationToken);
        await RunPass(context, definition, LinkPass, rows, cancellationToken);
        await RunPass(context, definition, DeactivatePass, rows, cancellationToken);

        // Assert — the link survives, which is the reason deactivation runs last
        await using var assertContext = _fixture.CreateContext();
        var leaver = await assertContext.Employees.SingleAsync(e => e.EmployeeNumber == "E-5001", cancellationToken);
        var report = await assertContext.Employees.SingleAsync(e => e.EmployeeNumber == "E-5002", cancellationToken);

        leaver.IsActive.Should().BeFalse();
        report.ManagerId.Should().Be(leaver.Id);
    }

    /// <summary>
    /// Deleting an employee takes their addresses with them — the FK cascade only the real provider enforces.
    /// </summary>
    [Fact]
    public async Task DeletingAnEmployee_CascadesToTheirWorkEmails()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);

        await using (var context = _fixture.CreateContext())
        {
            var definition = CreateDefinition(context);
            await RunPass(context, definition, CreatePass,
                Rows(definition, Employee("E-3004", "Riley", "Nakamura", "riley3@acme.example",
                    additionalEmails: ["riley3@acme-legacy.example"])), cancellationToken);
        }

        // Act — hard delete, bypassing the soft-delete interceptor, to exercise the FK itself
        await using (var deleteContext = _fixture.CreateContext())
        {
            await deleteContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM Organization.Employees WHERE EmployeeNumber = 'E-3004'", cancellationToken);
        }

        // Assert
        await using var assertContext = _fixture.CreateContext();
        var remaining = await assertContext.Set<Wayd.Common.Domain.Employees.EmployeeEmail>()
            .CountAsync(cancellationToken);
        remaining.Should().Be(0);
    }
}
