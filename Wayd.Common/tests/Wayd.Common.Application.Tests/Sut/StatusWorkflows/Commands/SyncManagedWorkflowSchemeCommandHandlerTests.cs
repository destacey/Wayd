using FluentAssertions;
using Wayd.Common.Application.StatusWorkflows.Commands;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows.Commands;

/// <summary>The sync keeping a managed scheme in line with its source process.</summary>
public sealed class SyncManagedWorkflowSchemeCommandHandlerTests : StatusWorkflowHandlerTestBase
{
    private static readonly WorkflowOwnerDescriptor Synced = new(
        "test.sync-scheme.item", "Item", new Dictionary<int, WorkflowAlias>(), [], hasRecordTypes: true, hasManagedWorkflows: true);

    private static readonly SourceStatus New = new("new", "New", StatusCategory.Proposed);
    private static readonly SourceStatus Closed = new("closed", "Closed", StatusCategory.Done);

    public SyncManagedWorkflowSchemeCommandHandlerTests() => WorkflowOwners.Register(Synced);

    private SyncManagedWorkflowSchemeCommandHandler CreateSut() =>
        new(DbContext, DateTimeProvider.Object, Logger<SyncManagedWorkflowSchemeCommandHandler>());

    private StatusWorkflow SeedWorkflow(string key)
    {
        var workflow = StatusWorkflow.CreateManaged(key, Synced.Key, key, [New, Closed], EventActor.Sync(null), Now).Value;
        workflow.ClearDomainEvents();
        DbContext.AddStatusWorkflow(workflow);

        return workflow;
    }

    private RecordType SeedType(string name)
    {
        var recordType = RecordType.CreateManaged(Synced.Key, name, name, EventActor.Sync(null), Now).Value;
        recordType.ClearDomainEvents();
        DbContext.AddRecordType(recordType);

        return recordType;
    }

    private static SyncManagedWorkflowSchemeCommand Command(Guid defaultWorkflowId, params ManagedSchemeEntry[] entries) =>
        new(Synced.Key, "azure-devops:agile", "Agile", defaultWorkflowId, entries);

    [Fact]
    public async Task Handle_ShouldCreateAManagedSchemeWithItsEntries_TheFirstTime()
    {
        // Arrange
        var bugFlow = SeedWorkflow("agile:bug");
        var storyFlow = SeedWorkflow("agile:story");
        var bug = SeedType("Bug");
        var story = SeedType("Story");

        // Act
        var result = await CreateSut().Handle(
            Command(bugFlow.Id, new(bug.Id, bugFlow.Id), new(story.Id, storyFlow.Id)), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var scheme = DbContext.StatusWorkflowSchemes.Single(s => s.Id == result.Value);
        (scheme.Ownership, scheme.ExternalKey, scheme.Name, scheme.DefaultWorkflowId).Should().Be(
            (WorkflowOwnership.Managed, "azure-devops:agile", "Agile", bugFlow.Id));
        scheme.WorkflowFor(bug.Id).Should().Be(bugFlow.Id);
        scheme.WorkflowFor(story.Id).Should().Be(storyFlow.Id);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldUpdateTheExistingScheme_ByItsExternalKey()
    {
        // Arrange
        var bugFlow = SeedWorkflow("agile:bug");
        var storyFlow = SeedWorkflow("agile:story");
        var bug = SeedType("Bug");
        var story = SeedType("Story");
        var scheme = StatusWorkflowScheme.CreateManaged(
            Synced.Key, "Agile", "azure-devops:agile", bugFlow, EventActor.Sync(null), Now, [(bug, bugFlow)]).Value;
        DbContext.AddStatusWorkflowScheme(scheme);

        // Act
        var result = await CreateSut().Handle(
            Command(bugFlow.Id, new(bug.Id, bugFlow.Id), new(story.Id, storyFlow.Id)), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().Be(scheme.Id);
        DbContext.StatusWorkflowSchemes.Should().ContainSingle();
        scheme.WorkflowFor(story.Id).Should().Be(storyFlow.Id);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenARecordTypeDoesNotExist()
    {
        // Arrange
        var bugFlow = SeedWorkflow("agile:bug");

        // Act
        var result = await CreateSut().Handle(
            Command(bugFlow.Id, new ManagedSchemeEntry(Guid.NewGuid(), bugFlow.Id)), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Record type not found.");
        DbContext.StatusWorkflowSchemes.Should().BeEmpty();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheDefaultWorkflowDoesNotExist()
    {
        // Act
        var result = await CreateSut().Handle(Command(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Status workflow not found.");
        DbContext.StatusWorkflowSchemes.Should().BeEmpty();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheExistingSchemeRefusesTheSource()
    {
        // Arrange
        var bugFlow = SeedWorkflow("agile:bug");
        var bug = SeedType("Bug");
        var scheme = StatusWorkflowScheme.CreateManaged(
            Synced.Key, "Agile", "azure-devops:agile", bugFlow, EventActor.Sync(null), Now, [(bug, bugFlow)]).Value;
        DbContext.AddStatusWorkflowScheme(scheme);
        var otherFlow = SeedWorkflow("agile:other");

        // Act
        var result = await CreateSut().Handle(
            Command(bugFlow.Id, new(bug.Id, bugFlow.Id), new(bug.Id, otherFlow.Id)), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        scheme.WorkflowFor(bug.Id).Should().Be(bugFlow.Id);
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
