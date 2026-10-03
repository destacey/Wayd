using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.AppIntegrations;
using Wayd.Common.Domain.Data;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.AppIntegration;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Common.Models;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Migrators.MSSQL.Migrations;
using Wayd.Infrastructure.Persistence.Activities;
using Wayd.Infrastructure.Persistence.Context;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves each baseline the Backfill-Employee-And-Identity-Mapping-Baseline-Activity migration writes is the entry
/// <see cref="ActivityLogEntryFactory"/> builds for the same baseline event.
/// </summary>
/// <remarks>
/// The payloads are hand-built T-SQL, so only a real SQL Server shows they read back the way the serializer writes
/// them.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class EmployeeAndIdentityMappingBaselineBackfillTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 15, 9, 30, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task Backfill_WritesTheEmployeeBaselineTheFactoryWould(bool withManager, bool isActive)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        Guid? managerId = withManager ? (await Seed(ct, _ => Task.FromResult(CreateEmployee(null, true)))).Id : null;
        var employee = await Seed(ct, _ => Task.FromResult(CreateEmployee(managerId, isActive)));

        // Act
        await RunBackfill(ct);

        // Assert
        await AssertBaseline(employee, (recordCreatedOn, timestamp) => new EmployeeBaselinedEvent(
            employee.Id, employee.Key, managerId, isActive, recordCreatedOn, null, timestamp), ct);
    }

    [Fact]
    public async Task Backfill_SkipsADeletedEmployee()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var employee = await Seed(ct, _ => Task.FromResult(CreateEmployee(null, true)));
        await using (var context = _fixture.CreateContext())
        {
            context.Employees.Remove(await context.Employees.SingleAsync(e => e.Id == employee.Id, ct));
            await context.SaveChangesAsync(ct);
        }

        // Act
        await RunBackfill(ct);

        // Assert
        await using var verify = _fixture.CreateContext();
        (await verify.ActivityLogs.AnyAsync(a => a.AggregateId == employee.Id, ct)).Should().BeFalse();
    }

    [Theory]
    [InlineData(ExternalIdentityMappingStatus.Unmapped)]
    [InlineData(ExternalIdentityMappingStatus.AutoMatched)]
    [InlineData(ExternalIdentityMappingStatus.ManuallyMapped)]
    [InlineData(ExternalIdentityMappingStatus.Ignored)]
    public async Task Backfill_WritesTheIdentityMappingBaselineTheFactoryWould(ExternalIdentityMappingStatus status)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var employee = await Seed(ct, _ => Task.FromResult(CreateEmployee(null, true)));
        var mapping = await Seed(ct, _ => Task.FromResult(CreateMapping(status, employee.Id)));

        // Act
        await RunBackfill(ct);

        // Assert
        await AssertBaseline(mapping, (recordCreatedOn, timestamp) => new ExternalIdentityMappingBaselinedEvent(
            mapping.Id, mapping.Connector, mapping.ConnectionId, mapping.EmployeeId, mapping.Status,
            recordCreatedOn, null, timestamp), ct);
    }

    [Fact]
    public async Task Backfill_MappingKeyedOnAnAddress_WritesNoAddress()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        const string address = "jordan.lee@acme.example";
        var mapping = await Seed(ct, _ => Task.FromResult(ExternalIdentityMapping.CreateUnmapped(
            Connector.AzureDevOps, Guid.NewGuid(), address, address, null, null, EventActor.System, Now)));

        // Act
        await RunBackfill(ct);

        // Assert
        await using var verify = _fixture.CreateContext();
        var entry = await verify.ActivityLogs.AsNoTracking().SingleAsync(a => a.AggregateId == mapping.Id, ct);
        entry.Payload.Should().NotContain("@");
    }

    [Fact]
    public async Task Backfill_RunTwice_WritesOneBaseline()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var employee = await Seed(ct, _ => Task.FromResult(CreateEmployee(null, true)));
        await RunBackfill(ct);

        // Act
        await RunBackfill(ct);

        // Assert
        await using var verify = _fixture.CreateContext();
        (await verify.ActivityLogs.CountAsync(a => a.AggregateId == employee.Id, ct)).Should().Be(1);
    }

    private static Employee CreateEmployee(Guid? managerId, bool isActive)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        return Employee.Create(new PersonName("Avery", null, "Chen"), $"E-{suffix}", null,
            new EmailAddress($"avery.{suffix}@acme.example"), "Engineer", "Delivery", null, managerId, isActive, null,
            EventActor.System, Now);
    }

    private static ExternalIdentityMapping CreateMapping(ExternalIdentityMappingStatus status, Guid employeeId)
    {
        var connectionId = Guid.NewGuid();
        var externalId = Guid.NewGuid().ToString();
        const string address = "avery.chen@acme.example";

        if (status == ExternalIdentityMappingStatus.AutoMatched)
            return ExternalIdentityMapping.CreateAutoMatched(Connector.AzureDevOps, connectionId, externalId, address,
                "Avery \"AC\" Chen", null, employeeId, EventActor.System, Now);

        var mapping = ExternalIdentityMapping.CreateUnmapped(Connector.AzureDevOps, connectionId, externalId, address,
            "Avery \"AC\" Chen", null, EventActor.System, Now);

        if (status == ExternalIdentityMappingStatus.ManuallyMapped)
            mapping.MapToEmployee(employeeId, EventActor.System, Now);
        else if (status == ExternalIdentityMappingStatus.Ignored)
            mapping.Ignore(EventActor.System, Now);

        return mapping;
    }

    /// <summary>
    /// Saves the record, then removes the entries its save wrote, leaving it as every such record was when the
    /// migration ran: on file with no history.
    /// </summary>
    private async Task<TEntity> Seed<TEntity>(CancellationToken ct, Func<WaydDbContext, Task<TEntity>> create)
        where TEntity : BaseEntity<Guid>
    {
        await using var context = _fixture.CreateContext();
        var entity = await create(context);

        context.Add(entity);
        await context.SaveChangesAsync(ct);

        await context.ActivityLogs.Where(a => a.AggregateId == entity.Id).ExecuteDeleteAsync(ct);

        return entity;
    }

    private async Task RunBackfill(CancellationToken ct)
    {
        // Straight through ADO: the payload's braces would be read as format placeholders by ExecuteSqlRaw.
        await using var context = _fixture.CreateContext();
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = BackfillEmployeeAndIdentityMappingBaselineActivity.UpSql;
        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task AssertBaseline<TEntity>(TEntity entity, Func<Instant?, Instant, DomainEvent> baseline, CancellationToken ct)
        where TEntity : BaseEntity<Guid>
    {
        await using var verify = _fixture.CreateContext();
        var entry = await verify.ActivityLogs.AsNoTracking()
            .SingleAsync(a => a.AggregateId == entity.Id, ct);
        var recordCreatedOn = await verify.Set<TEntity>().IgnoreQueryFilters()
            .Where(e => e.Id == entity.Id)
            .Select(e => EF.Property<Instant>(e, "SystemCreated"))
            .SingleAsync(ct);

        var expectedEvent = baseline(recordCreatedOn, entry.Timestamp);
        var expected = ActivityLogEntryFactory.CreateActivityLogEntry(expectedEvent, entity, 0, null);

        entry.EventId.Should().Be(expectedEvent.EventId);
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
            .Should().BeTrue($"the backfilled payload {entry.Payload} should match the serialized event {expected.Payload}");
    }
}
