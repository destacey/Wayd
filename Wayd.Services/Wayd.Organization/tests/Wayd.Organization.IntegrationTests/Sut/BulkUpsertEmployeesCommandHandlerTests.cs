using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Employees.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Models;
using Wayd.Organization.IntegrationTests.Infrastructure;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="BulkUpsertEmployeesCommandHandler"/> against a real SQL Server container.
/// The connector sync path is where the work-email collection is reconciled, and its correctness depends on
/// constraints only a real database enforces — the unique index on <c>EmployeeEmails.Email</c> above all. A
/// fake DbContext hands back fully-populated graphs, so it cannot catch a missing <c>Include</c>: the
/// collection simply looks loaded.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class BulkUpsertEmployeesCommandHandlerTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public BulkUpsertEmployeesCommandHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    private static BulkUpsertEmployeesCommandHandler CreateHandler(Wayd.Infrastructure.Persistence.Context.WaydDbContext context)
    {
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(d => d.Now).Returns(SqlServerDbContextFixture.FixedNow);

        return new BulkUpsertEmployeesCommandHandler(context, dateTimeProvider.Object, NullLogger<BulkUpsertEmployeesCommandHandler>.Instance);
    }

    private static async Task<Result<BulkUpsertEmployeesResult>> RunSync(
        SqlServerDbContextFixture fixture,
        IExternalEmployee[] payload,
        CancellationToken cancellationToken,
        EmployeeMatchProperty matchBy = EmployeeMatchProperty.Email)
    {
        await using var context = fixture.CreateContext();
        return await CreateHandler(context).Handle(
            new BulkUpsertEmployeesCommand(payload, Connector.Workday, "acme", matchBy),
            cancellationToken);
    }

    /// <summary>
    /// Regression test for the duplicate-key failure a real Entra full sync hit: the second run must
    /// recognize the addresses the first run wrote instead of re-inserting them.
    /// </summary>
    [Fact]
    public async Task Handle_RunTwice_DoesNotReinsertTheSameWorkEmails()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);

        IExternalEmployee[] payload =
        [
            External("E-9001", "avery.chen@acme.example"),
            External("E-9002", "jordan.blake@acme.example"),
        ];

        var first = await RunSync(_fixture, payload, cancellationToken);
        first.IsSuccess.Should().BeTrue(first.IsFailure ? first.Error : null);

        // Act — the same payload again, exactly as a scheduled full sync would send it.
        var second = await RunSync(_fixture, payload, cancellationToken);

        // Assert
        second.IsSuccess.Should().BeTrue(second.IsFailure ? second.Error : null);

        await using var assertContext = _fixture.CreateContext();
        var emails = await assertContext.Set<Wayd.Common.Domain.Employees.EmployeeEmail>()
            .ToListAsync(cancellationToken);
        emails.Should().HaveCount(2, "each employee keeps exactly one row across repeated syncs");
    }

    [Fact]
    public async Task Handle_RunTwiceWithAdditionalAddresses_KeepsTheCollectionStable()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);

        IExternalEmployee[] payload =
        [
            External("E-9003", "sam.ortiz@acme.example", "sam.ortiz@acme-legacy.example"),
        ];

        var first = await RunSync(_fixture, payload, cancellationToken);
        first.IsSuccess.Should().BeTrue(first.IsFailure ? first.Error : null);

        // Act
        var second = await RunSync(_fixture, payload, cancellationToken);

        // Assert
        second.IsSuccess.Should().BeTrue(second.IsFailure ? second.Error : null);

        await using var assertContext = _fixture.CreateContext();
        var employee = await assertContext.Employees
            .Include(e => e.Emails)
            .SingleAsync(e => e.EmployeeNumber == "E-9003", cancellationToken);

        employee.Emails.Select(e => e.Email.Value).Should().BeEquivalentTo(
            ["sam.ortiz@acme.example", "sam.ortiz@acme-legacy.example"]);
    }

    /// <summary>
    /// The source dropping an address has to delete the row — the reconcile only works if the existing
    /// collection was loaded in the first place.
    /// </summary>
    [Fact]
    public async Task Handle_SourceStopsReportingAnAddress_RemovesIt()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);

        var first = await RunSync(
            _fixture,
            [External("E-9004", "riley@acme.example", "riley@acme-legacy.example")],
            cancellationToken);
        first.IsSuccess.Should().BeTrue(first.IsFailure ? first.Error : null);

        // Act — the legacy address is gone from the source.
        var second = await RunSync(_fixture, [External("E-9004", "riley@acme.example")], cancellationToken);

        // Assert
        second.IsSuccess.Should().BeTrue(second.IsFailure ? second.Error : null);

        await using var assertContext = _fixture.CreateContext();
        var employee = await assertContext.Employees
            .Include(e => e.Emails)
            .SingleAsync(e => e.EmployeeNumber == "E-9004", cancellationToken);

        employee.Emails.Select(e => e.Email.Value).Should().BeEquivalentTo(["riley@acme.example"]);
    }

    /// <summary>
    /// The tenant-migration shape end to end: the primary moves to a new address while the previous one
    /// stays behind as a secondary.
    /// </summary>
    [Fact]
    public async Task Handle_PrimaryAddressChanges_MovesTheFlagAndKeepsTheFormerAddress()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);

        var first = await RunSync(_fixture, [External("E-9005", "casey@acme-legacy.example")], cancellationToken);
        first.IsSuccess.Should().BeTrue(first.IsFailure ? first.Error : null);

        // Act — matched by EmployeeNumber, since the email no longer matches.
        var second = await RunSync(
            _fixture,
            [External("E-9005", "casey@acme.example", "casey@acme-legacy.example")],
            cancellationToken);

        // Assert
        second.IsSuccess.Should().BeTrue(second.IsFailure ? second.Error : null);

        await using var assertContext = _fixture.CreateContext();
        var employee = await assertContext.Employees
            .Include(e => e.Emails)
            .SingleAsync(e => e.EmployeeNumber == "E-9005", cancellationToken);

        employee.Email.Value.Should().Be("casey@acme.example");
        employee.Emails.Single(e => e.IsPrimary).Email.Value.Should().Be("casey@acme.example");
        employee.Emails.Select(e => e.Email.Value).Should().Contain("casey@acme-legacy.example");
    }

    /// <summary>
    /// One employee leaves an address and another takes it in the same run, so both updates reach one save.
    /// The unique indexes are checked per statement, so this passes only if the vacating row is written first.
    /// </summary>
    [Fact]
    public async Task Handle_AddressHandedToAnotherEmployeeInOneRun_Saves()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);

        var seeded = await RunSync(
            _fixture,
            [External("E-9010", "shared@acme.example"), External("E-9011", "other@acme.example")],
            cancellationToken,
            EmployeeMatchProperty.EmployeeNumber);
        seeded.IsSuccess.Should().BeTrue(seeded.IsFailure ? seeded.Error : null);

        // Act — the vacating record comes first, so the taking record finds the address free.
        var result = await RunSync(
            _fixture,
            [External("E-9010", "moved@acme.example"), External("E-9011", "shared@acme.example")],
            cancellationToken,
            EmployeeMatchProperty.EmployeeNumber);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Conflicts.Should().BeEmpty();

        await using var assertContext = _fixture.CreateContext();
        var emails = await assertContext.Employees
            .ToDictionaryAsync(e => e.EmployeeNumber, e => e.Email.Value, cancellationToken);
        emails.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["E-9010"] = "moved@acme.example",
            ["E-9011"] = "shared@acme.example",
        });
    }

    /// <summary>
    /// The employee-number counterpart of the address handover: one row leaves a number and another takes it.
    /// </summary>
    [Fact]
    public async Task Handle_EmployeeNumberHandedToAnotherEmployeeInOneRun_Saves()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);

        var seeded = await RunSync(
            _fixture,
            [External("E-9020", "first@acme.example"), External("E-9021", "second@acme.example")],
            cancellationToken);
        seeded.IsSuccess.Should().BeTrue(seeded.IsFailure ? seeded.Error : null);

        // Act
        var result = await RunSync(
            _fixture,
            [External("E-9022", "first@acme.example"), External("E-9020", "second@acme.example")],
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Conflicts.Should().BeEmpty();

        await using var assertContext = _fixture.CreateContext();
        var numbers = await assertContext.Employees
            .ToDictionaryAsync(e => e.Email.Value, e => e.EmployeeNumber, cancellationToken);
        numbers.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["first@acme.example"] = "E-9022",
            ["second@acme.example"] = "E-9020",
        });
    }

    /// <summary>
    /// The key conflict against the real unique indexes: the conflicting address must be left off both
    /// <c>Employees.Email</c> and <c>EmployeeEmails.Email</c>, or the save fails for the whole batch.
    /// </summary>
    [Fact]
    public async Task Handle_KeysMatchDifferentEmployees_SavesWithoutTheConflictingAddress()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);

        var seeded = await RunSync(
            _fixture,
            [External("E-9030", "holder@acme.example"), External("E-9031", "matched@acme.example")],
            cancellationToken,
            EmployeeMatchProperty.EmployeeNumber);
        seeded.IsSuccess.Should().BeTrue(seeded.IsFailure ? seeded.Error : null);

        // Act — E-9031's source record now carries the address E-9030 holds.
        var result = await RunSync(
            _fixture,
            [External("E-9031", "holder@acme.example", "alias@acme.example")],
            cancellationToken,
            EmployeeMatchProperty.EmployeeNumber);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Conflicts.Should().ContainKey("E-9031");

        await using var assertContext = _fixture.CreateContext();
        var matched = await assertContext.Employees
            .Include(e => e.Emails)
            .SingleAsync(e => e.EmployeeNumber == "E-9031", cancellationToken);
        matched.IsActive.Should().BeTrue();
        matched.Email.Value.Should().Be("matched@acme.example");
        matched.Emails.Select(e => e.Email.Value).Should().BeEquivalentTo(["matched@acme.example", "alias@acme.example"]);
    }

    private static ExternalEmployeeRecord External(string employeeNumber, string email, params string[] additionalEmails) =>
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
            IsActive = true,
            EmployeeType = "Employee",
            Emails =
            [
                new ExternalEmployeeEmail(new EmailAddress(email), IsPrimary: true),
                .. additionalEmails.Select(a => new ExternalEmployeeEmail(new EmailAddress(a), IsPrimary: false)),
            ],
        };

    private sealed record ExternalEmployeeRecord : IExternalEmployee
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
