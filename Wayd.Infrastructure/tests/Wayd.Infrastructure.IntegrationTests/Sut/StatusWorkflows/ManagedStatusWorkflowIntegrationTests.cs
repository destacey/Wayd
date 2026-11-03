using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;

namespace Wayd.Infrastructure.IntegrationTests.Sut.StatusWorkflows;

/// <summary>
/// A managed workflow through real saves: its ownership and source keys persist, and the indexes let a
/// retired status keep its name while a new one takes it.
/// </summary>
/// <remarks>
/// The status name index is filtered to active statuses and the source key index to keyed ones, which only
/// SQL Server enforces; the in-memory fakes accept anything.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class ManagedStatusWorkflowIntegrationTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 1, 10, 0, 0);

    private static readonly WorkflowOwnerDescriptor Synced = new(
        "test.integration.synced", "Synced", new Dictionary<int, WorkflowAlias>(), [], [StatusCategory.Done], hasManagedWorkflows: true);

    private static readonly WorkflowOwnerDescriptor SyncedTypes = new(
        "test.integration.synced-types", "Synced Type", new Dictionary<int, WorkflowAlias>(), [], hasRecordTypes: true, hasManagedWorkflows: true);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task SyncFromSource_ShouldPersistARetiredStatus_BesideANewOneWithItsName()
    {
        // Arrange
        WorkflowOwners.Register(Synced);
        var ct = TestContext.Current.CancellationToken;
        var externalKey = $"conn-{Guid.CreateVersion7()}:agile:bug";

        var workflow = StatusWorkflow.CreateManaged(
            "Agile: Bug", Synced.Key, externalKey,
            [new("new", "New", StatusCategory.Proposed), new("active", "Active", StatusCategory.Active)],
            EventActor.Sync(null), Now).Value;

        await using (var setup = _fixture.CreateContext())
        {
            setup.StatusWorkflows.Add(workflow);
            await setup.SaveChangesAsync(ct);
        }

        var retiredId = workflow.Statuses.Single(s => s.ExternalKey == "active").Id;

        // Act
        await using (var sync = _fixture.CreateContext())
        {
            var stored = await sync.StatusWorkflows.Include(w => w.Statuses).SingleAsync(w => w.Id == workflow.Id, ct);
            stored.SyncFromSource(
                "Agile: Bug",
                [new("new", "New", StatusCategory.Proposed), new("active-2", "Active", StatusCategory.Active)],
                EventActor.Sync(null), Now).IsSuccess.Should().BeTrue();
            await sync.SaveChangesAsync(ct);
        }

        // Assert
        await using var context = _fixture.CreateContext();
        var reloaded = await context.StatusWorkflows.Include(w => w.Statuses)
            .SingleAsync(w => w.OwnerType == Synced.Key && w.ExternalKey == externalKey, ct);

        reloaded.Ownership.Should().Be(WorkflowOwnership.Managed);
        reloaded.State.Should().Be(StatusWorkflowState.Published);
        reloaded.Statuses.Select(s => (s.ExternalKey, s.Name, s.IsRetired)).Should().Equal(
            ("new", "New", false), ("active-2", "Active", false), ("active", "Active", true));
        reloaded.Statuses.Single(s => s.Id == retiredId).IsRetired.Should().BeTrue("records holding it still resolve it by id");
        reloaded.CheckIssues().Should().ContainSingle().Which.Severity.Should().Be(WorkflowCheckSeverity.Warned);
    }

    [Theory]
    [InlineData("swapped names", "Open", "Ready", true)]
    [InlineData("a name taken from a status the sync retires", "Ready", null, false)]
    public async Task SyncFromSource_ShouldSave_WhenNamesMoveBetweenStatuses(string because, string firstName, string? secondName, bool secondStays)
    {
        // Arrange
        WorkflowOwners.Register(Synced);
        var ct = TestContext.Current.CancellationToken;
        var externalKey = $"conn-{Guid.CreateVersion7()}:jira:story";

        var workflow = StatusWorkflow.CreateManaged(
            "Jira: Story", Synced.Key, externalKey,
            [new("10001", "Open", StatusCategory.Proposed), new("10002", "Ready", StatusCategory.Proposed)],
            EventActor.Sync(null), Now).Value;

        await using (var setup = _fixture.CreateContext())
        {
            setup.StatusWorkflows.Add(workflow);
            await setup.SaveChangesAsync(ct);
        }

        List<SourceStatus> source = [new("10001", firstName, StatusCategory.Proposed)];
        if (secondStays)
        {
            source.Add(new("10002", secondName!, StatusCategory.Proposed));
        }

        // Act
        await using (var sync = _fixture.CreateContext())
        {
            var stored = await sync.StatusWorkflows.Include(w => w.Statuses).SingleAsync(w => w.Id == workflow.Id, ct);
            stored.SyncFromSource("Jira: Story", source, EventActor.Sync(null), Now).IsSuccess.Should().BeTrue();
            await sync.SaveChangesAsync(ct);
        }

        // Assert
        await using var context = _fixture.CreateContext();
        var reloaded = await context.StatusWorkflows.Include(w => w.Statuses).SingleAsync(w => w.Id == workflow.Id, ct);
        reloaded.Statuses.Single(s => s.ExternalKey == "10001").Name.Should().Be(firstName, because);
        reloaded.Statuses.Single(s => s.ExternalKey == "10002").IsRetired.Should().Be(!secondStays, because);
    }

    [Fact]
    public async Task CreateManaged_ShouldBeRefusedByTheDatabase_WhenTheSourceKeyIsTaken()
    {
        // Arrange
        WorkflowOwners.Register(Synced);
        var ct = TestContext.Current.CancellationToken;
        var externalKey = $"conn-{Guid.CreateVersion7()}:agile:task";

        StatusWorkflow Mirror() => StatusWorkflow.CreateManaged(
            "Agile: Task", Synced.Key, externalKey, [new("new", "New", StatusCategory.Proposed)], EventActor.Sync(null), Now).Value;

        await using (var setup = _fixture.CreateContext())
        {
            setup.StatusWorkflows.Add(Mirror());
            await setup.SaveChangesAsync(ct);
        }

        await using var context = _fixture.CreateContext();
        context.StatusWorkflows.Add(Mirror());

        // Act
        var act = () => context.SaveChangesAsync(ct);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>("a source identifies each of its workflows once");
    }

    [Fact]
    public async Task RecordTypeName_ShouldBeFreed_WhenItsTypeIsRetired()
    {
        // Arrange
        WorkflowOwners.Register(SyncedTypes);
        var ct = TestContext.Current.CancellationToken;
        var connection = $"conn-{Guid.CreateVersion7()}";

        var retired = RecordType.CreateManaged(SyncedTypes.Key, connection, $"{connection}:bug", EventActor.Sync(null), Now).Value;
        retired.RetireFromSource(EventActor.Sync(null), Now);

        await using (var setup = _fixture.CreateContext())
        {
            setup.RecordTypes.Add(retired);
            await setup.SaveChangesAsync(ct);
        }

        await using var context = _fixture.CreateContext();
        context.RecordTypes.Add(RecordType.CreateManaged(SyncedTypes.Key, connection, $"{connection}:bug-2", EventActor.Sync(null), Now).Value);

        // Act
        var act = () => context.SaveChangesAsync(ct);

        // Assert
        await act.Should().NotThrowAsync("only active record types hold a name");
    }
}
