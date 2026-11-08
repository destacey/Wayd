using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Infrastructure.StatusWorkflows;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// The work item migrator against SQL Server: a selection names items by workspace and by their work type's
/// record type, which only a real query through the type can show.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class WorkItemStatusRecordMigratorTests(SqlServerDbContextFixture fixture)
{
    private const string SystemId = "work-item-migrator-test";
    private static readonly Instant Now = Instant.FromUtc(2026, 3, 2, 9, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    /// <summary>
    /// Workspaces A and B with an assignment each and C with none, a work item of each on the "from" workflow,
    /// and a "to" workflow with the same statuses.
    /// </summary>
    private sealed record Scenario(Guid A, Guid B, Guid C, StatusWorkflow From, StatusWorkflow To, Guid RecordTypeId, Guid ItemA, Guid ItemB, Guid ItemC);

    private async Task<Scenario> Seed(CancellationToken ct)
    {
        await _fixture.ResetWorkData(ct);
        var a = await WorkItemHistorySeeder.SeedWorkspace(_fixture, SystemId, "MIGA", ct);
        var b = await WorkItemHistorySeeder.SeedSiblingWorkspace(_fixture, a, "MIGB", ct);
        var c = await WorkItemHistorySeeder.SeedSiblingWorkspace(_fixture, a, "MIGC", ct);
        SourceStatus[] statuses =
        [
            new("state-1", "New", StatusCategory.Proposed),
            new("state-2", "Done", StatusCategory.Done),
        ];
        var from = await WorkItemHistorySeeder.SeedWorkflow(_fixture, a, [a, b], statuses, ct);
        var to = await WorkItemWorkflowSeeder.Seed(_fixture, WorkItemHistorySeeder.WorkTypeName, [], statuses, ct);

        var itemA = await WorkItemHistorySeeder.SeedWorkItem(_fixture, a, 1, from.Status("New"), Now, null, ct);
        var itemB = await WorkItemHistorySeeder.SeedWorkItem(_fixture, b, 2, from.Status("Done"), Now, Now, ct);
        var itemC = await WorkItemHistorySeeder.SeedWorkItem(_fixture, c, 3, from.Status("New"), Now, null, ct);

        await using var context = _fixture.CreateContext();
        var recordTypeId = await context.WorkTypes.Where(t => t.Name == WorkItemHistorySeeder.WorkTypeName).Select(t => t.RecordTypeId!.Value).SingleAsync(ct);

        return new Scenario(a, b, c, from, to, recordTypeId, itemA, itemB, itemC);
    }

    private async Task<Dictionary<Guid, Guid>> WorkflowsOfItems(CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        return await context.WorkItems.AsNoTracking().ToDictionaryAsync(w => w.Id, w => w.StatusWorkflowId, ct);
    }

    [Fact]
    public async Task Migrate_ForOneWorkspaceAndType_MovesOnlyThoseItems()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scenario = await Seed(ct);
        await using var context = _fixture.CreateContext();
        var sut = new WorkItemStatusRecordMigrator(context, context);
        var remap = StatusRemap.AutoMap(scenario.From, scenario.To).Value;

        // Act
        var migrated = await sut.Migrate(remap, new RecordSelection([scenario.A], scenario.RecordTypeId, []), EventActor.System, Now, ct);
        await context.SaveChangesAsync(ct);

        // Assert
        migrated.Value.Should().Be(1);
        var workflows = await WorkflowsOfItems(ct);
        workflows[scenario.ItemA].Should().Be(scenario.To.Id);
        workflows[scenario.ItemB].Should().Be(scenario.From.Id);
        workflows[scenario.ItemC].Should().Be(scenario.From.Id);

        await using var reader = _fixture.CreateContext();
        var moved = await reader.WorkItems.AsNoTracking().SingleAsync(w => w.Id == scenario.ItemA, ct);
        moved.StatusId.Should().Be(scenario.To.Status("New").Id);
        (await reader.StatusTransitions.CountAsync(t => t.RecordId == scenario.ItemA, ct)).Should().Be(0, "a managed record writes no transitions");
    }

    [Fact]
    public async Task Migrate_ForTheOrganization_MovesOnlyWorkspacesWithoutAnAssignment()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scenario = await Seed(ct);
        await using var context = _fixture.CreateContext();
        var sut = new WorkItemStatusRecordMigrator(context, context);
        var remap = StatusRemap.AutoMap(scenario.From, scenario.To).Value;

        // Act
        var migrated = await sut.Migrate(remap, new RecordSelection([null], scenario.RecordTypeId, []), EventActor.System, Now, ct);
        await context.SaveChangesAsync(ct);

        // Assert
        migrated.Value.Should().Be(1);
        var workflows = await WorkflowsOfItems(ct);
        workflows[scenario.ItemC].Should().Be(scenario.To.Id);
        workflows[scenario.ItemA].Should().Be(scenario.From.Id);
        workflows[scenario.ItemB].Should().Be(scenario.From.Id);
    }

    [Fact]
    public async Task Migrate_ForTheDefault_MovesTypesWithoutAnEntryButNotThoseWithOne()
    {
        // Arrange — the seeded type has an entry; a second type has no record type at all
        var ct = TestContext.Current.CancellationToken;
        var scenario = await Seed(ct);
        var untypedItem = await SeedItemOfAnUnlinkedType(scenario.A, 4, scenario.From.Status("New"), ct);
        await using var context = _fixture.CreateContext();
        var sut = new WorkItemStatusRecordMigrator(context, context);
        var remap = StatusRemap.AutoMap(scenario.From, scenario.To).Value;

        // Act
        var migrated = await sut.Migrate(remap, new RecordSelection([scenario.A], null, [scenario.RecordTypeId]), EventActor.System, Now, ct);
        await context.SaveChangesAsync(ct);

        // Assert
        migrated.Value.Should().Be(1);
        var workflows = await WorkflowsOfItems(ct);
        workflows[untypedItem].Should().Be(scenario.To.Id);
        workflows[scenario.ItemA].Should().Be(scenario.From.Id);
    }

    [Fact]
    public async Task Migrate_AnIncompleteRemap_Fails()
    {
        // Arrange — the target has no Done status, so the remap cannot place every status
        var ct = TestContext.Current.CancellationToken;
        var scenario = await Seed(ct);
        var partial = await WorkItemWorkflowSeeder.Seed(_fixture, WorkItemHistorySeeder.WorkTypeName, [],
            [new SourceStatus("state-9", "Started", StatusCategory.Active)], ct);
        await using var context = _fixture.CreateContext();
        var sut = new WorkItemStatusRecordMigrator(context, context);
        var remap = StatusRemap.AutoMap(scenario.From, partial).Value;

        // Act
        var migrated = await sut.Migrate(remap, new RecordSelection([scenario.A], scenario.RecordTypeId, []), EventActor.System, Now, ct);

        // Assert
        migrated.IsFailure.Should().BeTrue();
    }

    /// <summary>An item of a second work type the sync never linked to a record type.</summary>
    private async Task<Guid> SeedItemOfAnUnlinkedType(Guid workspaceId, int externalId, WorkflowStatus status, CancellationToken ct)
    {
        await using (var context = _fixture.CreateContext())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                IF NOT EXISTS (SELECT 1 FROM [Work].[WorkTypes] WHERE [Name] = 'Unlinked Story')
                    INSERT INTO [Work].[WorkTypes] ([Name], [IsActive], [IsDeleted], [LevelId], [SystemCreated], [SystemLastModified])
                    SELECT 'Unlinked Story', 1, 0, [LevelId], SYSUTCDATETIME(), SYSUTCDATETIME()
                    FROM [Work].[WorkTypes] WHERE [Name] = {WorkItemHistorySeeder.WorkTypeName};
                """,
                ct);
        }

        var id = await WorkItemHistorySeeder.SeedWorkItem(_fixture, workspaceId, externalId, status, Now, null, ct);

        await using (var context = _fixture.CreateContext())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [Work].[WorkItems] SET [TypeId] = (SELECT [Id] FROM [Work].[WorkTypes] WHERE [Name] = 'Unlinked Story') WHERE [Id] = {id};", ct);
        }

        return id;
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 0)]
    public async Task CountByStatus_ForTheDefault_SkipsTypesWithAnEntry(bool typeHasEntry, int expected)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scenario = await Seed(ct);
        await using var context = _fixture.CreateContext();
        var sut = new WorkItemStatusRecordMigrator(context, context);
        Guid[] excluded = typeHasEntry ? [scenario.RecordTypeId] : [];

        // Act
        var counts = await sut.CountByStatus(scenario.From.Id, new RecordSelection([scenario.A, scenario.B], null, excluded), ct);

        // Assert
        counts.Value.Values.Sum().Should().Be(expected);
        if (!typeHasEntry)
        {
            counts.Value[scenario.From.Status("New").Id].Should().Be(1);
            counts.Value[scenario.From.Status("Done").Id].Should().Be(1);
        }
    }
}
