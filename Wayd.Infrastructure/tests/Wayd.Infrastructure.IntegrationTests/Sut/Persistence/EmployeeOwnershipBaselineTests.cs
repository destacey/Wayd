using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Wayd.AppIntegration.Domain.Models.Workday;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Common.Models;
using Wayd.Infrastructure.DataProtection;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Migrators.MSSQL.Migrations;
using Wayd.Infrastructure.Persistence.Activities;
using Wayd.Infrastructure.Persistence.Context;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves the Baseline-Employee-Ownership migration hands employees to the active people connection's system and
/// records each as the entry a live <see cref="EmployeeAdoptedEvent"/> would have written.
/// </summary>
/// <remarks>
/// The migration writes every employee, so each test runs in a transaction it rolls back, leaving the shared
/// database as it found it.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class EmployeeOwnershipBaselineTests(SqlServerDbContextFixture fixture)
{
    private const string WsdlUrl = "https://wd.acme.example/ccx/service/acme_corp/Staffing/v46.1?wsdl";
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 15, 9, 30, 0);

    private readonly SqlServerDbContextFixture _fixture = InstallSecretProtector(fixture);

    /// <summary>
    /// Connections encrypt their credentials on save, through a protector the host installs at startup. Installed
    /// here only when nothing has, so a protector another test set is never swapped from under it.
    /// </summary>
    private static SqlServerDbContextFixture InstallSecretProtector(SqlServerDbContextFixture fixture)
    {
        try
        {
            _ = SecretProtectorAccessor.Current;
        }
        catch (InvalidOperationException)
        {
            SecretProtectorAccessor.Set(new AesGcmSecretProtector(RandomNumberGenerator.GetBytes(32)));
        }

        return fixture;
    }

    private static async Task<IDbContextTransaction> BeginIsolated(WaydDbContext context, CancellationToken ct)
    {
        var transaction = await context.Database.BeginTransactionAsync(ct);

        // Other tests' connections share the database; the migration acts only when exactly one is active.
        await context.Connections
            .Where(c => (c.Connector == Connector.Entra || c.Connector == Connector.Workday) && c.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsActive, false), ct);

        return transaction;
    }

    private static async Task<Employee> SeedOwnedEmployee(WaydDbContext context, CancellationToken ct, LocalDate? accessEndDate = null)
    {
        var number = $"B{Random.Shared.Next(100_000, 999_999)}";
        var employee = Employee.Create(
            new PersonName("Baseline", null, number), number, null, new EmailAddress($"{number}@acme.example"),
            null, null, null, null, isActive: true, employeeType: null, EventActor.System, Now,
            accessEndDate: accessEndDate);
        context.Employees.Add(employee);
        await context.SaveChangesAsync(ct);

        return employee;
    }

    private static async Task SeedActiveWorkdayConnection(WaydDbContext context, CancellationToken ct)
    {
        context.WorkdayConnections.Add(WorkdayConnection.Create(
            "Workers", null, new WorkdayConnectionConfiguration(WsdlUrl, "isu-user", "isu-pass"), true, EventActor.System, Now));
        await context.SaveChangesAsync(ct);
    }

    private static async Task RunBaseline(WaydDbContext context, CancellationToken ct)
    {
        // Straight through ADO: the payload's braces would be read as format placeholders by ExecuteSqlRaw.
        var connection = context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = context.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = BaselineEmployeeOwnership.BaselineOwnershipSql;
        await command.ExecuteNonQueryAsync(ct);
    }

    [Fact]
    public async Task Baseline_WithAnActivePeopleConnection_WritesTheEntryALiveAdoptWould()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        await using var transaction = await BeginIsolated(context, ct);
        await SeedActiveWorkdayConnection(context, ct);
        var accessEnd = new LocalDate(2027, 3, 31);
        var employee = await SeedOwnedEmployee(context, ct, accessEnd);

        // Act
        await RunBaseline(context, ct);

        // Assert
        var row = await context.Employees.AsNoTracking().SingleAsync(e => e.Id == employee.Id, ct);
        row.IsManagedBy(Connector.Workday, "acme_corp").Should().BeTrue();
        row.OwnershipInfo.ExternalId.Should().Be(employee.EmployeeNumber);
        row.AccessEndDate.Should().BeNull();

        var entry = await context.ActivityLogs.AsNoTracking()
            .SingleAsync(a => a.AggregateId == employee.Id && a.EventType == nameof(EmployeeAdoptedEvent), ct);
        var liveEvent = new EmployeeAdoptedEvent(
            employee.Id, Ownership.Owned, null, null, accessEnd, Ownership.Managed, Connector.Workday, "acme_corp",
            EventActor.System, entry.Timestamp)
        {
            EventId = entry.EventId,
        };
        var expected = ActivityLogEntryFactory.CreateActivityLogEntry(liveEvent, liveEvent, 0, null);

        entry.Should().BeEquivalentTo(expected, options => options
            .Including(e => e.EventType)
            .Including(e => e.Category)
            .Including(e => e.EventVersion)
            .Including(e => e.DomainArea)
            .Including(e => e.AggregateType)
            .Including(e => e.AggregateId)
            .Including(e => e.ActorKind)
            .Including(e => e.UserId)
            .Including(e => e.EmployeeId)
            .Including(e => e.Ordinal)
            .Including(e => e.CorrelationId)
            .Including(e => e.Summary));
        JsonNode.DeepEquals(JsonNode.Parse(entry.Payload), JsonNode.Parse(expected.Payload))
            .Should().BeTrue($"the baselined payload {entry.Payload} should match the serialized event {expected.Payload}");

        await transaction.RollbackAsync(ct);
    }

    [Fact]
    public async Task Baseline_RunTwice_WritesOneEntry()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        await using var transaction = await BeginIsolated(context, ct);
        await SeedActiveWorkdayConnection(context, ct);
        var employee = await SeedOwnedEmployee(context, ct);
        await RunBaseline(context, ct);

        // Act
        await RunBaseline(context, ct);

        // Assert
        (await context.ActivityLogs.CountAsync(a => a.AggregateId == employee.Id && a.EventType == nameof(EmployeeAdoptedEvent), ct))
            .Should().Be(1);

        await transaction.RollbackAsync(ct);
    }

    [Fact]
    public async Task Baseline_WithoutAnActivePeopleConnection_LeavesEmployeesOwned()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        await using var transaction = await BeginIsolated(context, ct);
        var employee = await SeedOwnedEmployee(context, ct);

        // Act
        await RunBaseline(context, ct);

        // Assert
        var row = await context.Employees.AsNoTracking().SingleAsync(e => e.Id == employee.Id, ct);
        row.IsManaged.Should().BeFalse();
        (await context.ActivityLogs.AnyAsync(a => a.AggregateId == employee.Id && a.EventType == nameof(EmployeeAdoptedEvent), ct))
            .Should().BeFalse();

        await transaction.RollbackAsync(ct);
    }
}
