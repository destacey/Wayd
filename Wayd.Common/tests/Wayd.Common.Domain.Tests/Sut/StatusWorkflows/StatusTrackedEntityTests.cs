using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.Tests.Sut.StatusWorkflows;

public sealed class StatusTrackedEntityTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 15, 9, 30, 0);

    private const int WorkingAlias = 31;

    private static readonly WorkflowOwnerDescriptor Ticket = new(
        "test.tracked.ticket",
        "Ticket",
        new Dictionary<int, WorkflowAlias> { [WorkingAlias] = new("Working", StatusCategory.Active) },
        [],
        hasRecordTypes: true);

    public StatusTrackedEntityTests() => WorkflowOwners.Register(Ticket);

    /// <summary>A record of the test owner type, exposing the protected status methods.</summary>
    private sealed class TicketRecord : StatusTrackedEntity
    {
        public override string StatusOwnerType => Ticket.Key;

        public Result Apply(StatusRef status) => ApplyStatus(status, EventActor.System, Now);

        public Result ChangeType(StatusWorkflow target, Guid? chosenStatusId = null) =>
            SwitchRecordTypeWorkflow(target, chosenStatusId, EventActor.System, Now, "Became a Bug");
    }

    private static StatusWorkflow Workflow(string name, params (string Name, StatusCategory Category, int Alias)[] statuses)
    {
        var workflow = StatusWorkflow.Create(name, null, Ticket.Key, EventActor.System, Now).Value;
        foreach (var (statusName, category, alias) in statuses)
        {
            workflow.AddStatus(statusName, null, category, alias, EventActor.System, Now);
        }
        workflow.Publish(EventActor.System, Now);

        return workflow;
    }

    private static StatusWorkflow StoryFlow() => Workflow(
        "Story Flow",
        ("New", StatusCategory.Proposed, StatusWorkflow.NoAlias),
        ("Doing", StatusCategory.Active, WorkingAlias),
        ("Closed", StatusCategory.Done, StatusWorkflow.NoAlias));

    private static StatusWorkflow BugFlow() => Workflow(
        "Bug Flow",
        ("Triage", StatusCategory.Proposed, StatusWorkflow.NoAlias),
        ("Verifying", StatusCategory.Active, StatusWorkflow.NoAlias),
        ("Fixing", StatusCategory.Active, WorkingAlias),
        ("Fixed", StatusCategory.Done, StatusWorkflow.NoAlias));

    private static TicketRecord RecordIn(StatusWorkflow workflow, string statusName)
    {
        var record = new TicketRecord();
        record.Apply(workflow.TargetRef(workflow.Statuses.Single(s => s.Name == statusName).Id)!);
        record.DrainStatusTransitions();
        record.ClearDomainEvents();

        return record;
    }

    #region SwitchRecordTypeWorkflow

    [Fact]
    public void SwitchRecordTypeWorkflow_ShouldLandOnTheStatusWithTheSameAlias()
    {
        // Arrange
        var story = StoryFlow();
        var bug = BugFlow();
        var record = RecordIn(story, "Doing");

        // Act
        var result = record.ChangeType(bug);

        // Assert
        // Verifying comes first among Active statuses; the alias decides.
        result.IsSuccess.Should().BeTrue();
        record.StatusWorkflowId.Should().Be(bug.Id);
        record.StatusName.Should().Be("Fixing");
    }

    [Fact]
    public void SwitchRecordTypeWorkflow_ShouldLandOnTheFirstStatusOfTheSameCategory_WithoutAnAlias()
    {
        // Arrange
        var story = StoryFlow();
        var bug = BugFlow();
        var record = RecordIn(story, "Closed");

        // Act
        var result = record.ChangeType(bug);

        // Assert
        result.IsSuccess.Should().BeTrue();
        record.StatusName.Should().Be("Fixed");
        record.StatusCategory.Should().Be(StatusCategory.Done);
    }

    [Fact]
    public void SwitchRecordTypeWorkflow_ShouldRecordTheSwitch()
    {
        // Arrange
        var story = StoryFlow();
        var bug = BugFlow();
        var record = RecordIn(story, "Doing");
        var fromStatusId = record.StatusId;

        // Act
        record.ChangeType(bug);

        // Assert
        var transition = record.DrainStatusTransitions().Should().ContainSingle().Subject;
        transition.FromStatusId.Should().Be(fromStatusId);
        transition.WorkflowId.Should().Be(bug.Id);
        transition.Reason.Should().Be("Became a Bug");
        var switched = record.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<StatusWorkflowSwitchedEvent>().Subject;
        switched.FromStatusId.Should().Be(fromStatusId);
        switched.ToWorkflowId.Should().Be(bug.Id);
    }

    [Fact]
    public void SwitchRecordTypeWorkflow_ShouldFail_WhenNothingMatches_AndNoStatusIsChosen()
    {
        // Arrange
        var story = StoryFlow();
        var noDone = Workflow(
            "Intake Only",
            ("Received", StatusCategory.Proposed, StatusWorkflow.NoAlias),
            ("Doing", StatusCategory.Active, WorkingAlias));
        var record = RecordIn(story, "Closed");

        // Act
        var result = record.ChangeType(noDone);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("'Intake Only' has no status matching 'Closed'. Choose the status this record moves to.");
        record.StatusWorkflowId.Should().Be(story.Id);
        record.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void SwitchRecordTypeWorkflow_ShouldUseTheChosenStatus_OverTheRule()
    {
        // Arrange
        var story = StoryFlow();
        var bug = BugFlow();
        var record = RecordIn(story, "Doing");
        var triage = bug.Statuses.Single(s => s.Name == "Triage");

        // Act
        var result = record.ChangeType(bug, triage.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        record.StatusId.Should().Be(triage.Id);
    }

    [Fact]
    public void SwitchRecordTypeWorkflow_ShouldFail_WhenTheChosenStatusIsFromAnotherWorkflow()
    {
        // Arrange
        var story = StoryFlow();
        var bug = BugFlow();
        var record = RecordIn(story, "Doing");
        var storyStatus = story.Statuses.Single(s => s.Name == "New");

        // Act
        var result = record.ChangeType(bug, storyStatus.Id);

        // Assert
        result.IsFailure.Should().BeTrue();
        record.StatusWorkflowId.Should().Be(story.Id);
    }

    [Fact]
    public void SwitchRecordTypeWorkflow_ShouldDoNothing_WhenTheNewTypeFollowsTheSameWorkflow()
    {
        // Arrange
        var story = StoryFlow();
        var record = RecordIn(story, "Doing");
        var statusId = record.StatusId;

        // Act
        var result = record.ChangeType(story);

        // Assert
        result.IsSuccess.Should().BeTrue();
        record.StatusId.Should().Be(statusId);
        record.DrainStatusTransitions().Should().BeEmpty();
        record.DomainEvents.Should().BeEmpty();
    }

    #endregion SwitchRecordTypeWorkflow
}
