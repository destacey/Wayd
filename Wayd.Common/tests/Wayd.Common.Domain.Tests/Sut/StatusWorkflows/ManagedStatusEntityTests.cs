using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.Tests.Sut.StatusWorkflows;

public sealed class ManagedStatusEntityTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 15, 9, 30, 0);

    private static readonly WorkflowOwnerDescriptor Item = new(
        "test.managed.item",
        "Item",
        new Dictionary<int, WorkflowAlias>(),
        [],
        supportsRestrictedTransitions: true,
        hasManagedWorkflows: true);

    public ManagedStatusEntityTests() => WorkflowOwners.Register(Item);

    /// <summary>A synced record of the test owner type, exposing the protected status method.</summary>
    private sealed class ItemRecord : ManagedStatusEntity
    {
        public override string StatusOwnerType => Item.Key;

        public bool Apply(StatusRef status) => ApplySourceStatus(status);
    }

    private static StatusWorkflow SourceWorkflow(string externalKey) =>
        StatusWorkflow.CreateManaged(
            externalKey,
            Item.Key,
            externalKey,
            [
                new SourceStatus("new", "New", StatusCategory.Proposed),
                new SourceStatus("active", "Active", StatusCategory.Active),
                new SourceStatus("closed", "Closed", StatusCategory.Done),
            ],
            EventActor.Sync(null),
            Now).Value;

    /// <summary>
    /// An owned workflow allowing only New → Active, and Active → Closed only with a reason.
    /// </summary>
    private static StatusWorkflow RestrictedWorkflow()
    {
        var workflow = StatusWorkflow.Create("Restricted", null, Item.Key, EventActor.System, Now).Value;
        var proposed = workflow.AddStatus("New", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, Now).Value;
        var active = workflow.AddStatus("Active", null, StatusCategory.Active, StatusWorkflow.NoAlias, EventActor.System, Now).Value;
        var done = workflow.AddStatus("Closed", null, StatusCategory.Done, StatusWorkflow.NoAlias, EventActor.System, Now).Value;
        Require(workflow.SetTransitionMode(TransitionMode.Restricted, EventActor.System, Now));
        Require(workflow.SetTransitions(
            [new(proposed.Id, active.Id), new(active.Id, done.Id, [TransitionConditionKind.RequiresReason])],
            EventActor.System,
            Now));
        Require(workflow.Publish(EventActor.System, Now));

        return workflow;
    }

    /// <summary>Fails the setup loudly, so a broken arrangement does not read as the behaviour under test.</summary>
    private static void Require(Result result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Test setup failed: {result.Error}");
        }
    }

    private static StatusRef Target(StatusWorkflow workflow, string name) =>
        workflow.TargetRef(workflow.Statuses.Single(s => s.Name == name).Id)!;

    [Fact]
    public void ApplySourceStatus_ShouldTakeTheReportedStatus_WhenTheRecordHasNone()
    {
        // Arrange
        var workflow = SourceWorkflow("conn-1:agile:bug");
        var record = new ItemRecord();
        var active = workflow.Statuses.Single(s => s.ExternalKey == "active");

        // Act
        var changed = record.Apply(Target(workflow, "Active"));

        // Assert
        changed.Should().BeTrue();
        record.StatusId.Should().Be(active.Id);
        record.StatusWorkflowId.Should().Be(workflow.Id);
        record.StatusName.Should().Be("Active");
        record.StatusCategory.Should().Be(StatusCategory.Active);
        record.StatusAliasValue.Should().Be(StatusWorkflow.NoAlias);
    }

    [Fact]
    public void ApplySourceStatus_ShouldNotBeRefused_WhenTheWorkflowIsRestricted()
    {
        // Arrange
        var workflow = RestrictedWorkflow();
        var record = new ItemRecord();
        record.Apply(Target(workflow, "New"));

        // Act
        // New → Closed is not an allowed move, and the move into Closed needs a reason.
        var changed = record.Apply(Target(workflow, "Closed"));

        // Assert
        changed.Should().BeTrue();
        record.StatusName.Should().Be("Closed");
        record.StatusCategory.Should().Be(StatusCategory.Done);
    }

    [Fact]
    public void ApplySourceStatus_ShouldRaiseNothing_WhenTheStatusChanges()
    {
        // Arrange
        var workflow = SourceWorkflow("conn-1:agile:bug");
        var record = new ItemRecord();
        record.Apply(Target(workflow, "New"));

        // Act
        record.Apply(Target(workflow, "Closed"));

        // Assert
        record.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ApplySourceStatus_ShouldReportNoChange_WhenNothingAboutTheStatusChanged()
    {
        // Arrange
        var workflow = SourceWorkflow("conn-1:agile:bug");
        var record = new ItemRecord();
        record.Apply(Target(workflow, "Active"));

        // Act
        var changed = record.Apply(Target(workflow, "Active"));

        // Assert
        changed.Should().BeFalse();
        record.StatusName.Should().Be("Active");
    }

    [Fact]
    public void ApplySourceStatus_ShouldMoveOntoAnotherWorkflow_WhenTheSourceReportsOne()
    {
        // Arrange
        var bug = SourceWorkflow("conn-1:agile:bug");
        var story = SourceWorkflow("conn-1:agile:story");
        var record = new ItemRecord();
        record.Apply(Target(bug, "Active"));

        // Act
        var changed = record.Apply(Target(story, "Closed"));

        // Assert
        changed.Should().BeTrue();
        record.StatusWorkflowId.Should().Be(story.Id);
        record.StatusId.Should().Be(story.Statuses.Single(s => s.ExternalKey == "closed").Id);
    }

    [Fact]
    public void ApplySourceStatus_ShouldTakeTheNewCategory_WhenAnAdministratorReclassifiedTheHeldStatus()
    {
        // Arrange
        var workflow = SourceWorkflow("conn-1:agile:bug");
        var record = new ItemRecord();
        record.Apply(Target(workflow, "Active"));
        var active = workflow.Statuses.Single(s => s.ExternalKey == "active");
        Require(workflow.ReclassifyStatus(active.Id, StatusCategory.Done, StatusWorkflow.NoAlias, EventActor.System, Now));

        // Act
        var changed = record.Apply(Target(workflow, "Active"));

        // Assert
        changed.Should().BeTrue();
        record.StatusId.Should().Be(active.Id);
        record.StatusCategory.Should().Be(StatusCategory.Done);
    }

    [Fact]
    public void ApplySourceStatus_ShouldTakeTheNewName_WhenTheSourceRenamedTheHeldStatus()
    {
        // Arrange
        var workflow = SourceWorkflow("conn-1:agile:bug");
        var record = new ItemRecord();
        record.Apply(Target(workflow, "Active"));
        Require(workflow.SyncFromSource(
            workflow.Name,
            [
                new SourceStatus("new", "New", StatusCategory.Proposed),
                new SourceStatus("active", "In Progress", StatusCategory.Active),
                new SourceStatus("closed", "Closed", StatusCategory.Done),
            ],
            EventActor.Sync(null),
            Now));

        // Act
        var changed = record.Apply(Target(workflow, "In Progress"));

        // Assert
        changed.Should().BeTrue();
        record.StatusName.Should().Be("In Progress");
    }

    [Fact]
    public void ApplyRemap_ShouldMoveTheRecordOntoTheTargetWorkflow()
    {
        // Arrange
        var bug = SourceWorkflow("conn-1:agile:bug");
        var story = SourceWorkflow("conn-1:agile:story");
        var record = new ItemRecord();
        record.Apply(Target(bug, "Active"));
        var remap = StatusRemap.AutoMap(bug, story).Value;

        // Act
        var result = record.ApplyRemap(remap);

        // Assert
        result.IsSuccess.Should().BeTrue();
        record.StatusWorkflowId.Should().Be(story.Id);
        record.StatusId.Should().Be(story.Statuses.Single(s => s.ExternalKey == "active").Id);
        record.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ApplyRemap_ShouldChangeNothing_WhenTheRecordIsAlreadyOnTheTargetWorkflow()
    {
        // Arrange
        var bug = SourceWorkflow("conn-1:agile:bug");
        var story = SourceWorkflow("conn-1:agile:story");
        var record = new ItemRecord();
        record.Apply(Target(story, "Closed"));
        var remap = StatusRemap.AutoMap(bug, story).Value;

        // Act
        var result = record.ApplyRemap(remap);

        // Assert
        result.IsSuccess.Should().BeTrue();
        record.StatusId.Should().Be(story.Statuses.Single(s => s.ExternalKey == "closed").Id);
    }

    [Fact]
    public void ApplyRemap_ShouldFail_WhenTheRecordHoldsAStatusOfNeitherWorkflow()
    {
        // Arrange
        var bug = SourceWorkflow("conn-1:agile:bug");
        var story = SourceWorkflow("conn-1:agile:story");
        var task = SourceWorkflow("conn-1:agile:task");
        var record = new ItemRecord();
        record.Apply(Target(task, "Active"));
        var remap = StatusRemap.AutoMap(bug, story).Value;

        // Act
        var result = record.ApplyRemap(remap);

        // Assert
        result.IsFailure.Should().BeTrue();
        record.StatusWorkflowId.Should().Be(task.Id);
    }

    [Fact]
    public void ApplyRemap_ShouldFail_WhenTheRemapIsIncomplete()
    {
        // Arrange
        var bug = SourceWorkflow("conn-1:agile:bug");
        var narrow = StatusWorkflow.CreateManaged(
            "Narrow",
            Item.Key,
            "conn-1:agile:narrow",
            [
                new SourceStatus("open-a", "Open A", StatusCategory.Proposed),
                new SourceStatus("open-b", "Open B", StatusCategory.Proposed),
                new SourceStatus("done", "Done", StatusCategory.Done),
            ],
            EventActor.Sync(null),
            Now).Value;
        var record = new ItemRecord();
        record.Apply(Target(bug, "Closed"));
        var remap = StatusRemap.AutoMap(bug, narrow).Value;

        // Act
        var result = record.ApplyRemap(remap);

        // Assert
        remap.IsComplete.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        record.StatusWorkflowId.Should().Be(bug.Id);
    }
}
