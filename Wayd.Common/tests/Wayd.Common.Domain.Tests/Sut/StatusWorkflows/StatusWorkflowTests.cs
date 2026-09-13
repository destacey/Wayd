using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.Common.Domain.Tests.Sut.StatusWorkflows;

public sealed class StatusWorkflowTests
{
    // A fictional module's vocabulary. Deliberately not Product Management's: the engine has to work
    // for any module that registers a descriptor, and testing it against a real module's aliases would
    // let a coupling back to that module pass unnoticed.
    private const int NotableAlias = 11;
    private const int TerminalAlias = 12;

    private static readonly WorkflowOwnerDescriptor Widget = new(
        "test.widget",
        "Widget",
        new Dictionary<int, string> { [NotableAlias] = "Notable", [TerminalAlias] = "Terminal" },
        [NotableAlias, TerminalAlias]);

    public StatusWorkflowTests() => WorkflowOwners.Register(Widget);

    private static StatusWorkflow WidgetWorkflow()
    {
        var workflow = StatusWorkflow.Create("Widget Workflow", null, Widget.Key, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;
        workflow.AddStatus("Proposed", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));
        workflow.AddStatus("Notable", null, StatusCategory.Active, NotableAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));
        workflow.AddStatus("Terminal", null, StatusCategory.Done, TerminalAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        return workflow;
    }

    #region Create

    [Fact]
    public void Create_ShouldStartAsDraft()
    {
        // Arrange & Act
        var result = StatusWorkflow.Create("Widget Workflow", "For widgets.", Widget.Key, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.State.Should().Be(StatusWorkflowState.Draft);
        result.Value.OwnerType.Should().Be(Widget.Key);
        result.Value.IsSystem.Should().BeFalse();
        result.Value.Statuses.Should().BeEmpty();
    }

    [Fact]
    public void Create_ShouldFail_WhenTheOwnerTypeIsNotRegistered()
    {
        // Arrange & Act
        var result = StatusWorkflow.Create("Mystery Workflow", null, "test.not-registered", EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        // The cost of a string key over an enum: this is caught at runtime rather than by the compiler,
        // so it has to fail loudly rather than produce a workflow nothing can activate.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("'test.not-registered' is not a registered workflow owner type. Its module may not be registered.");
    }

    [Fact]
    public void Create_WithWhitespaceName_Throws()
    {
        // Arrange
        var name = "   ";

        // Act
        Action act = () => StatusWorkflow.Create(name, null, Widget.Key, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion Create

    #region AddStatus

    [Fact]
    public void AddStatus_ShouldAppendInOrder()
    {
        // Arrange
        var workflow = StatusWorkflow.Create("Widget Workflow", null, Widget.Key, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;

        // Act
        workflow.AddStatus("Proposed", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));
        workflow.AddStatus("Terminal", null, StatusCategory.Done, TerminalAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        workflow.Statuses.Should().HaveCount(2);
        workflow.Statuses.Select(s => s.Order).Should().BeInAscendingOrder();
        workflow.Statuses.Last().Alias.Should().Be(TerminalAlias);
    }

    [Fact]
    public void AddStatus_ShouldFail_WhenNameAlreadyUsed()
    {
        // Arrange
        var workflow = StatusWorkflow.Create("Widget Workflow", null, Widget.Key, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;
        workflow.AddStatus("Proposed", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var result = workflow.AddStatus("proposed", null, StatusCategory.Active, StatusWorkflow.NoAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A status named 'proposed' already exists in this workflow.");
    }

    [Fact]
    public void AddStatus_ShouldFail_WhenAliasAlreadyClaimed()
    {
        // Arrange
        var workflow = StatusWorkflow.Create("Widget Workflow", null, Widget.Key, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;
        workflow.AddStatus("Terminal", null, StatusCategory.Done, TerminalAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var result = workflow.AddStatus("Finished", null, StatusCategory.Done, TerminalAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        // Named through the module's own describer, so the message reads in its vocabulary rather than
        // as a bare number.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Another status in this workflow is already the 'Terminal' status.");
    }

    [Fact]
    public void AddStatus_ShouldAllowManyStatusesWithoutAlias()
    {
        // Arrange
        var workflow = StatusWorkflow.Create("Widget Workflow", null, Widget.Key, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;

        // Act
        workflow.AddStatus("Proposed", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));
        var result = workflow.AddStatus("In Progress", null, StatusCategory.Active, StatusWorkflow.NoAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        result.IsSuccess.Should().BeTrue();
        workflow.Statuses.Count(s => s.Alias == StatusWorkflow.NoAlias).Should().Be(2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddStatus_WithoutAName_ShouldFail(string? name)
    {
        // Arrange
        var workflow = StatusWorkflow.Create("Widget Workflow", null, Widget.Key, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;

        // Act
        var result = workflow.AddStatus(name!, null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        // A Result-returning method reports every failure the same way, rather than throwing for one
        // kind of bad input and returning a failure for another.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A status must have a name.");
    }

    [Fact]
    public void AddStatus_ShouldTrimTheName()
    {
        // Arrange
        var workflow = StatusWorkflow.Create("Widget Workflow", null, Widget.Key, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;

        // Act
        var result = workflow.AddStatus("  Proposed  ", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Proposed");
    }

    [Fact]
    public void AddStatus_ShouldFail_WhenWorkflowIsActive()
    {
        // Arrange
        var workflow = WidgetWorkflow();
        workflow.Publish(EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var result = workflow.AddStatus("Deferred", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only draft workflows can be restructured.");
    }

    #endregion AddStatus

    #region Publish

    [Fact]
    public void Publish_ShouldSucceed_WhenEveryRequiredAliasIsPresent()
    {
        // Arrange
        var workflow = WidgetWorkflow();

        // Act
        var result = workflow.Publish(EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        result.IsSuccess.Should().BeTrue();
        workflow.State.Should().Be(StatusWorkflowState.Published);
    }

    [Fact]
    public void Publish_ShouldFail_WhenARequiredAliasIsMissing()
    {
        // Arrange
        var workflow = StatusWorkflow.Create("Widget Workflow", null, Widget.Key, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;
        workflow.AddStatus("Proposed", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));
        workflow.AddStatus("Notable", null, StatusCategory.Active, NotableAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var result = workflow.Publish(EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        // Refused here rather than discovered later, deep inside an aggregate, on a record an
        // administrator has already created.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A Widget workflow needs a status for each of: Terminal.");
        workflow.State.Should().Be(StatusWorkflowState.Draft);
    }

    [Fact]
    public void Publish_ShouldFail_ListingEveryMissingAlias()
    {
        // Arrange
        var workflow = StatusWorkflow.Create("Widget Workflow", null, Widget.Key, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;
        workflow.AddStatus("Proposed", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var result = workflow.Publish(EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A Widget workflow needs a status for each of: Notable, Terminal.");
    }

    [Fact]
    public void Publish_ShouldFail_WhenAlreadyPublished()
    {
        // Arrange
        var workflow = WidgetWorkflow();
        workflow.Publish(EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var result = workflow.Publish(EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The workflow is already published.");
    }

    #endregion Publish

    #region RequiredAliases

    [Fact]
    public void RequiredAliases_ShouldComeFromTheOwningModulesDescriptor()
    {
        // Arrange
        var workflow = WidgetWorkflow();

        // Act
        var required = workflow.RequiredAliases;

        // Assert
        // The engine holds no table of its own — which meanings are mandatory is the module's call.
        required.Should().BeEquivalentTo([NotableAlias, TerminalAlias]);
    }

    #endregion RequiredAliases

    #region StatusFor

    [Fact]
    public void StatusFor_ShouldResolveByAlias_NotByName()
    {
        // Arrange
        var workflow = WidgetWorkflow();
        var terminal = workflow.StatusFor(TerminalAlias)!;

        // Act
        workflow.RenameStatus(terminal.Id, "Wrapped Up", "Renamed by an administrator.", EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        var resolved = workflow.StatusFor(TerminalAlias);
        resolved.Should().NotBeNull();
        resolved!.Id.Should().Be(terminal.Id);
        resolved.Name.Should().Be("Wrapped Up");
    }

    [Fact]
    public void StatusFor_ShouldReturnNull_ForNoAlias()
    {
        // Arrange
        var workflow = WidgetWorkflow();

        // Act
        var resolved = workflow.StatusFor(StatusWorkflow.NoAlias);

        // Assert
        resolved.Should().BeNull();
    }

    #endregion StatusFor

    #region InitialStatus

    [Fact]
    public void InitialStatus_ShouldBeTheLowestOrderedProposedStatus()
    {
        // Arrange
        var workflow = WidgetWorkflow();

        // Act
        var initial = workflow.InitialStatus;

        // Assert
        initial.Should().NotBeNull();
        initial!.Name.Should().Be("Proposed");
        initial.Category.Should().Be(StatusCategory.Proposed);
    }

    [Fact]
    public void InitialStatus_ShouldFallBackToLowestOrdered_WhenNoProposedStatusExists()
    {
        // Arrange
        var workflow = StatusWorkflow.Create("Widget Workflow", null, Widget.Key, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;
        workflow.AddStatus("Notable", null, StatusCategory.Active, NotableAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));
        workflow.AddStatus("Terminal", null, StatusCategory.Done, TerminalAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var initial = workflow.InitialStatus;

        // Assert
        initial.Should().NotBeNull();
        initial!.Name.Should().Be("Notable");
    }

    #endregion InitialStatus

    #region ReclassifyStatus

    private static readonly Instant At = Instant.FromUtc(2026, 1, 15, 9, 30, 0);

    [Fact]
    public void ReclassifyStatus_ShouldChangeTheCategoryAndRaiseTheEvent()
    {
        // Arrange
        var workflow = WidgetWorkflow();
        var status = workflow.Statuses.Single(s => s.Name == "Proposed");

        // Act
        var result = workflow.ReclassifyStatus(status.Id, StatusCategory.Active, StatusWorkflow.NoAlias, EventActor.System, At);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workflow.Statuses.Single(s => s.Name == "Proposed").Category.Should().Be(StatusCategory.Active);
        workflow.DomainEvents.OfType<WorkflowStatusReclassifiedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void ReclassifyStatus_ShouldSetAnAlias_SoAFailedPublishIsRecoverable()
    {
        // Without this the only way to add a missing required alias is to delete the status and add it
        // again, losing its description and its position.
        // Arrange
        var workflow = StatusWorkflow.Create("Widget Workflow", null, Widget.Key, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;
        workflow.AddStatus("Proposed", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));
        workflow.AddStatus("Notable", null, StatusCategory.Active, NotableAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));
        var terminal = workflow.AddStatus("Terminal", "The end of the line.", StatusCategory.Done, StatusWorkflow.NoAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;

        workflow.Publish(EventActor.System, At).IsFailure.Should().BeTrue("the Terminal alias is missing");

        // Act
        var result = workflow.ReclassifyStatus(terminal.Id, StatusCategory.Done, TerminalAlias, EventActor.System, At);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workflow.Publish(EventActor.System, At).IsSuccess.Should().BeTrue();
        workflow.Statuses.Single(s => s.Id == terminal.Id).Description.Should().Be("The end of the line.");
    }

    [Fact]
    public void ReclassifyStatus_ShouldRaiseNoEvent_WhenOnlyTheAliasChanges()
    {
        // The event exists to tell a consumer that records rolled up under a different category. An
        // alias change moves nothing.
        // Arrange
        var workflow = WidgetWorkflow();
        var status = workflow.Statuses.Single(s => s.Name == "Proposed");

        // Act
        var result = workflow.ReclassifyStatus(status.Id, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, At);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workflow.DomainEvents.OfType<WorkflowStatusReclassifiedEvent>().Should().BeEmpty();
    }

    [Fact]
    public void ReclassifyStatus_ShouldFail_WhenTheAliasIsAlreadyClaimed()
    {
        // Arrange
        var workflow = WidgetWorkflow();
        var proposed = workflow.Statuses.Single(s => s.Name == "Proposed");

        // Act
        var result = workflow.ReclassifyStatus(proposed.Id, StatusCategory.Done, TerminalAlias, EventActor.System, At);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Another status in this workflow is already the 'Terminal' status.");
    }

    [Fact]
    public void ReclassifyStatus_ShouldAllowAStatusToKeepItsOwnAlias()
    {
        // The uniqueness check must exclude the status being changed, or recategorising it without
        // touching its alias would collide with itself.
        // Arrange
        var workflow = WidgetWorkflow();
        var terminal = workflow.Statuses.Single(s => s.Name == "Terminal");

        // Act
        var result = workflow.ReclassifyStatus(terminal.Id, StatusCategory.Removed, TerminalAlias, EventActor.System, At);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workflow.Statuses.Single(s => s.Id == terminal.Id).Category.Should().Be(StatusCategory.Removed);
    }

    [Fact]
    public void ReclassifyStatus_ShouldFail_WhenTheWorkflowIsPublished()
    {
        // Records carry a denormalized category, so moving one on a live workflow leaves every record
        // holding it disagreeing with the status it points at. That is a remap, not an edit.
        // Arrange
        var workflow = WidgetWorkflow();
        workflow.Publish(EventActor.System, At);
        var status = workflow.Statuses.Single(s => s.Name == "Proposed");

        // Act
        var result = workflow.ReclassifyStatus(status.Id, StatusCategory.Active, StatusWorkflow.NoAlias, EventActor.System, At);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ReclassifyStatus_ShouldFail_ForAnUnknownStatus()
    {
        // Arrange
        var workflow = WidgetWorkflow();

        // Act
        var result = workflow.ReclassifyStatus(Guid.CreateVersion7(), StatusCategory.Active, StatusWorkflow.NoAlias, EventActor.System, At);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Status not found.");
    }

    #endregion ReclassifyStatus

    #region ReorderStatuses

    [Fact]
    public void ReorderStatuses_ShouldRepositionEveryStatus()
    {
        // Arrange
        var workflow = WidgetWorkflow();
        var reversed = workflow.Statuses.Reverse().Select(s => s.Id).ToList();

        // Act
        var result = workflow.ReorderStatuses(reversed, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        result.IsSuccess.Should().BeTrue();
        workflow.Statuses.Select(s => s.Id).Should().Equal(reversed);
    }

    [Fact]
    public void ReorderStatuses_ShouldFail_WhenTheListIsPartial()
    {
        // Arrange
        var workflow = WidgetWorkflow();
        var partial = workflow.Statuses.Take(2).Select(s => s.Id).ToList();

        // Act
        var result = workflow.ReorderStatuses(partial, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The supplied statuses must be exactly the statuses in this workflow.");
    }

    #endregion ReorderStatuses

    #region Clone

    [Fact]
    public void Clone_ShouldCopyStatusesWithNewIds()
    {
        // Arrange
        var workflow = WidgetWorkflow();
        workflow.Publish(EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var clone = workflow.Clone("Our Widget Workflow", null, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        clone.IsSystem.Should().BeFalse();
        clone.State.Should().Be(StatusWorkflowState.Draft);
        clone.OwnerType.Should().Be(workflow.OwnerType);
        clone.Statuses.Should().HaveCount(workflow.Statuses.Count);
        clone.Statuses.Select(s => s.Name).Should().Equal(workflow.Statuses.Select(s => s.Name));

        // New ids, so editing the clone cannot reach records using the original.
        clone.Statuses.Select(s => s.Id).Should().NotIntersectWith(workflow.Statuses.Select(s => s.Id));
        clone.Statuses.Should().OnlyContain(s => s.WorkflowId == clone.Id);
    }

    [Fact]
    public void Clone_ShouldPreserveAliases()
    {
        // Arrange
        var workflow = WidgetWorkflow();

        // Act
        var clone = workflow.Clone("Our Widget Workflow", null, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        clone.StatusFor(TerminalAlias).Should().NotBeNull();
        clone.Publish(EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Clone_ShouldProduceEditableStatuses()
    {
        // Arrange
        var workflow = WidgetWorkflow();

        // Act
        var clone = workflow.Clone("Our Widget Workflow", null, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        // Cloning is how an organization diverges from a seeded default, so nothing about the copy may
        // stay read-only.
        clone.IsSystem.Should().BeFalse();
        clone.Statuses.Should().OnlyContain(s => !s.IsSystem);
    }

    #endregion Clone

    #region System workflows

    [Fact]
    public void AddStatus_ShouldFail_OnASystemWorkflow()
    {
        // Arrange
        var workflow = StatusWorkflow.CreateSystem("Default Widget Workflow", null, Widget.Key, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;

        // Act
        var result = workflow.AddStatus("Proposed", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("System workflows cannot be modified. Clone this workflow to change it.");
    }

    [Fact]
    public void Update_ShouldFail_OnASystemWorkflow()
    {
        // Arrange
        var workflow = StatusWorkflow.CreateSystem("Default Widget Workflow", null, Widget.Key, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;

        // Act
        var result = workflow.Update("Renamed", null, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("System workflows cannot be modified. Clone this workflow to change it.");
    }

    [Fact]
    public void Publish_ShouldFail_OnASystemWorkflow()
    {
        // Arrange
        var workflow = StatusWorkflow.CreateSystem("Default Widget Workflow", null, Widget.Key, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;

        // Act
        var result = workflow.Publish(EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        // Seeded workflows are activated through PublishSystem by the seeder that builds them; leaving
        // the public path open would make that internal method pointless.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("System workflows are published by the seeder that creates them.");
    }

    [Fact]
    public void AddStatus_ShouldNotMarkTheStatusAsSystemOwned()
    {
        // Arrange
        var workflow = StatusWorkflow.Create("Widget Workflow", null, Widget.Key, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;

        // Act
        var result = workflow.AddStatus("Proposed", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        result.Value.IsSystem.Should().BeFalse();
    }

    #endregion System workflows

    #region Archive

    [Fact]
    public void Archive_ShouldFail_WhenTheWorkflowIsStillDraft()
    {
        // Arrange
        var workflow = WidgetWorkflow();

        // Act
        var result = workflow.Archive(isAssigned: false, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only published workflows can be archived.");
    }

    [Fact]
    public void Archive_ShouldWithdrawAPublishedWorkflow()
    {
        // Arrange
        var workflow = WidgetWorkflow();
        workflow.Publish(EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var result = workflow.Archive(isAssigned: false, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        result.IsSuccess.Should().BeTrue();
        workflow.State.Should().Be(StatusWorkflowState.Archived);
    }

    #endregion Archive

    #region Domain Events

    /// <summary>
    /// Stands in for the first save: assigns the key every event waits for, raises what was waiting on it,
    /// and clears it so a test sees only what its own act raises.
    /// </summary>
    private static StatusWorkflow Saved(StatusWorkflow workflow)
    {
        workflow.SetPrivate(w => w.Key, 7);
        workflow.ExecutePostPersistenceActions();
        workflow.ClearDomainEvents();

        return workflow;
    }

    [Fact]
    public void Create_RaisesCreated_OnceTheFirstSaveAssignsTheKey()
    {
        // Arrange & Act
        var workflow = StatusWorkflow.Create("Widget Workflow", "For widgets.", Widget.Key, EventActor.System, At).Value;

        // Assert
        workflow.DomainEvents.Should().BeEmpty();

        workflow.SetPrivate(w => w.Key, 7);
        workflow.ExecutePostPersistenceActions();

        var created = workflow.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<WorkflowCreatedEvent>().Subject;
        created.Key.Should().Be(7);
        created.Name.Should().Be("Widget Workflow");
        created.OwnerType.Should().Be(Widget.Key);
        created.SourceWorkflowId.Should().BeNull();
        created.Statuses.Should().BeEmpty();
    }

    [Fact]
    public void Clone_RaisesCreated_CarryingTheCopiedStatusesAndTheSource()
    {
        // Arrange
        var source = Saved(WidgetWorkflow());

        // Act
        var clone = source.Clone("Widget Workflow v2", null, EventActor.System, At);

        // Assert
        clone.SetPrivate(w => w.Key, 8);
        clone.ExecutePostPersistenceActions();

        var created = clone.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<WorkflowCreatedEvent>().Subject;
        created.SourceWorkflowId.Should().Be(source.Id);
        created.Statuses.Select(s => s.Name).Should().Equal("Proposed", "Notable", "Terminal");
        created.Statuses.Select(s => s.StatusId).Should().Equal(clone.Statuses.Select(s => s.Id));
    }

    [Fact]
    public void ASeededWorkflow_RecordsItsCreationBeforeTheStatusesAndThePublishThatFollowed()
    {
        // The seeder builds and publishes a workflow before the save that assigns its key.
        // Arrange
        var workflow = StatusWorkflow.CreateSystem("Default Widget Workflow", null, Widget.Key, EventActor.System, At).Value;
        workflow.AddSystemStatus("Notable", null, StatusCategory.Active, NotableAlias, EventActor.System, At);
        workflow.AddSystemStatus("Terminal", null, StatusCategory.Done, TerminalAlias, EventActor.System, At);

        // Act
        workflow.PublishSystem(EventActor.System, At);

        // Assert
        workflow.SetPrivate(w => w.Key, 7);
        workflow.ExecutePostPersistenceActions();

        workflow.DomainEvents.Select(e => e.GetType()).Should().Equal(
            typeof(WorkflowCreatedEvent),
            typeof(WorkflowStatusAddedEvent),
            typeof(WorkflowStatusAddedEvent),
            typeof(WorkflowPublishedEventV2));
        ((WorkflowCreatedEvent)workflow.DomainEvents.First()).Statuses.Should().BeEmpty("the statuses were added after it was created");
        workflow.DomainEvents.OfType<WorkflowPublishedEventV2>().Single().Key.Should().Be(7);
    }

    [Fact]
    public void Update_RaisesDetailsUpdated_CarryingWhatItReplaced()
    {
        // Arrange
        var workflow = Saved(WidgetWorkflow());

        // Act
        workflow.Update("Gadget Workflow", "Now for gadgets.", EventActor.System, At);

        // Assert
        var raised = workflow.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<WorkflowDetailsUpdatedEvent>().Subject;
        raised.Name.Should().Be("Gadget Workflow");
        raised.Description.Should().Be("Now for gadgets.");
        raised.Previous.Should().Be(new WorkflowDetails("Widget Workflow", null));
    }

    [Fact]
    public void Update_RaisesNothing_WhenOnlyWhitespaceDiffers()
    {
        // Arrange
        var workflow = Saved(WidgetWorkflow());

        // Act
        workflow.Update(" Widget Workflow ", "  ", EventActor.System, At);

        // Assert
        workflow.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AddStatus_RaisesStatusAdded()
    {
        // Arrange
        var workflow = Saved(WidgetWorkflow());

        // Act
        var status = workflow.AddStatus("Parked", "Waiting.", StatusCategory.Active, StatusWorkflow.NoAlias, EventActor.System, At).Value;

        // Assert
        var raised = workflow.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<WorkflowStatusAddedEvent>().Subject;
        raised.StatusId.Should().Be(status.Id);
        raised.Name.Should().Be("Parked");
        raised.Category.Should().Be(StatusCategory.Active);
        raised.Order.Should().Be(4);
    }

    [Fact]
    public void RemoveStatus_RaisesStatusRemoved_CarryingTheName()
    {
        // Arrange
        var workflow = Saved(WidgetWorkflow());
        var status = workflow.Statuses.Single(s => s.Name == "Proposed");

        // Act
        workflow.RemoveStatus(status.Id, EventActor.System, At);

        // Assert
        var raised = workflow.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<WorkflowStatusRemovedEvent>().Subject;
        raised.StatusId.Should().Be(status.Id);
        raised.Name.Should().Be("Proposed");
    }

    [Fact]
    public void RenameStatus_RaisesStatusRenamed_CarryingWhatItReplaced()
    {
        // Arrange
        var workflow = Saved(WidgetWorkflow());
        var status = workflow.Statuses.Single(s => s.Name == "Proposed");

        // Act
        workflow.RenameStatus(status.Id, "Idea", "Not yet started.", EventActor.System, At);

        // Assert
        var raised = workflow.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<WorkflowStatusRenamedEvent>().Subject;
        raised.Name.Should().Be("Idea");
        raised.Previous.Should().Be(new WorkflowStatusDetails("Proposed", null));
    }

    [Fact]
    public void RenameStatus_RaisesNothing_WhenOnlyWhitespaceDiffers()
    {
        // Arrange
        var workflow = Saved(WidgetWorkflow());
        var status = workflow.Statuses.Single(s => s.Name == "Proposed");

        // Act
        workflow.RenameStatus(status.Id, "Proposed ", " ", EventActor.System, At);

        // Assert
        workflow.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ReorderStatuses_RaisesStatusesReordered_WithBothOrders()
    {
        // Arrange
        var workflow = Saved(WidgetWorkflow());
        var previous = workflow.Statuses.Select(s => s.Id).ToList();
        var reversed = Enumerable.Reverse(previous).ToList();

        // Act
        workflow.ReorderStatuses(reversed, EventActor.System, At);

        // Assert
        var raised = workflow.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<WorkflowStatusesReorderedEvent>().Subject;
        raised.PreviousOrder.Should().Equal(previous);
        raised.Order.Should().Equal(reversed);
    }

    [Fact]
    public void ReorderStatuses_RaisesNothing_WhenTheOrderIsUnchanged()
    {
        // Arrange
        var workflow = Saved(WidgetWorkflow());

        // Act
        workflow.ReorderStatuses([.. workflow.Statuses.Select(s => s.Id)], EventActor.System, At);

        // Assert
        workflow.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ReclassifyStatus_RaisesOnlyAliasChanged_WhenTheCategoryStays()
    {
        // Arrange
        var workflow = Saved(WidgetWorkflow());
        var terminal = workflow.Statuses.Single(s => s.Name == "Terminal");

        // Act
        workflow.ReclassifyStatus(terminal.Id, StatusCategory.Done, StatusWorkflow.NoAlias, EventActor.System, At);

        // Assert
        var raised = workflow.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<WorkflowStatusAliasChangedEvent>().Subject;
        raised.StatusId.Should().Be(terminal.Id);
        raised.FromAlias.Should().Be(TerminalAlias);
        raised.ToAlias.Should().Be(StatusWorkflow.NoAlias);
    }

    #endregion Domain Events
}
