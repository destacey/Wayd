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
        [NotableAlias, TerminalAlias],
        supportsRestrictedTransitions: true);

    // An owner type whose records cannot be held to a restricted transition set.
    private static readonly WorkflowOwnerDescriptor Locked = new("test.locked", "Locked", new Dictionary<int, string>(), []);

    private const int ReadyAlias = 21;
    private const int ShippedAlias = 22;
    private const int PulledAlias = 23;

    // An owner type that moves only through named methods and declares the moves they make, as delivery
    // records do. Only pulling a shipped gadget accepts a condition.
    private static readonly WorkflowOwnerDescriptor Gadget = new(
        "test.gadget",
        "Gadget",
        new Dictionary<int, string> { [ReadyAlias] = "Ready", [ShippedAlias] = "Shipped", [PulledAlias] = "Pulled" },
        [ReadyAlias, ShippedAlias, PulledAlias],
        supportsRestrictedTransitions: true,
        moves:
        [
            WorkflowMove.Required("Prepare", fromAlias: null, ReadyAlias),
            WorkflowMove.Required("Ship", ReadyAlias, ShippedAlias),
            WorkflowMove.Optional("Dispatch", fromAlias: null, ShippedAlias),
            WorkflowMove.Required("Pull", ReadyAlias, PulledAlias),
            WorkflowMove.Optional("Pull", ShippedAlias, PulledAlias, TransitionConditionKind.RequiresReason),
        ]);

    public StatusWorkflowTests() => WorkflowOwners.Register(Widget, Gadget);

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

    #region Transitions

    // A widget workflow laid out as a line: Proposed → Notable → Terminal.
    private static StatusWorkflow RestrictedWidgetWorkflow()
    {
        var workflow = WidgetWorkflow();
        workflow.SetTransitionMode(TransitionMode.Restricted, EventActor.System, At);
        workflow.SetTransitions([Move(workflow, "Proposed", "Notable"), Move(workflow, "Notable", "Terminal")], EventActor.System, At);

        return workflow;
    }

    private static Guid StatusId(StatusWorkflow workflow, string name) => workflow.Statuses.Single(s => s.Name == name).Id;

    private static WorkflowTransitionDefinition Move(StatusWorkflow workflow, string from, string to, params TransitionConditionKind[] conditions) =>
        new(StatusId(workflow, from), StatusId(workflow, to), conditions);

    private static IEnumerable<(string From, string To)> Moves(StatusWorkflow workflow) =>
        workflow.Transitions.Select(t => (
            workflow.Statuses.Single(s => s.Id == t.FromStatusId).Name,
            workflow.Statuses.Single(s => s.Id == t.ToStatusId).Name));

    private static StatusWorkflow InState(StatusWorkflow workflow, StatusWorkflowState state)
    {
        if (state != StatusWorkflowState.Draft)
            workflow.Publish(EventActor.System, At).IsSuccess.Should().BeTrue();

        if (state == StatusWorkflowState.Archived)
            workflow.Archive(isAssigned: false, EventActor.System, At).IsSuccess.Should().BeTrue();

        return workflow;
    }

    [Fact]
    public void Create_ShouldAllowAnyMove()
    {
        // Arrange & Act
        var workflow = WidgetWorkflow();

        // Assert
        workflow.TransitionMode.Should().Be(TransitionMode.AnyToAny);
        workflow.Transitions.Should().BeEmpty();
        workflow.AllowedFrom(StatusId(workflow, "Terminal")).Should().BeNull();
    }

    [Fact]
    public void SetTransitionMode_ShouldRestrictTheWorkflow_AndRaiseTheChange()
    {
        // Arrange
        var workflow = Saved(WidgetWorkflow());

        // Act
        var result = workflow.SetTransitionMode(TransitionMode.Restricted, EventActor.System, At);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workflow.TransitionMode.Should().Be(TransitionMode.Restricted);

        var raised = workflow.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<WorkflowTransitionModeChangedEvent>().Subject;
        raised.Key.Should().Be(7);
        raised.FromMode.Should().Be(TransitionMode.AnyToAny);
        raised.ToMode.Should().Be(TransitionMode.Restricted);
    }

    [Fact]
    public void SetTransitionMode_RaisesNothing_WhenTheModeIsUnchanged()
    {
        // Arrange
        var workflow = Saved(WidgetWorkflow());

        // Act
        var result = workflow.SetTransitionMode(TransitionMode.AnyToAny, EventActor.System, At);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workflow.DomainEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData(StatusWorkflowState.Published)]
    [InlineData(StatusWorkflowState.Archived)]
    public void SetTransitionMode_ShouldFail_OnceTheWorkflowIsNoLongerADraft(StatusWorkflowState state)
    {
        // Arrange
        var workflow = InState(WidgetWorkflow(), state);

        // Act
        var result = workflow.SetTransitionMode(TransitionMode.Restricted, EventActor.System, At);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only draft workflows can be restructured.");
        workflow.TransitionMode.Should().Be(TransitionMode.AnyToAny);
    }

    [Fact]
    public void SetTransitionMode_ShouldFail_OnASystemWorkflow()
    {
        // Arrange
        var workflow = StatusWorkflow.CreateSystem("Default Widget Workflow", null, Widget.Key, EventActor.System, At).Value;

        // Act
        var result = workflow.SetTransitionMode(TransitionMode.Restricted, EventActor.System, At);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("System workflows cannot be modified. Clone this workflow to change it.");
    }

    [Fact]
    public void SetTransitions_ShouldReplaceTheSet_KeepingTheIdentityOfMovesItKept()
    {
        // Arrange
        var workflow = Saved(RestrictedWidgetWorkflow());
        var kept = workflow.Transitions.Single(t => t.FromStatusId == StatusId(workflow, "Proposed"));
        var dropped = workflow.Transitions.Single(t => t.FromStatusId == StatusId(workflow, "Notable"));

        // Act
        var result = workflow.SetTransitions(
            [Move(workflow, "Proposed", "Notable"), Move(workflow, "Proposed", "Terminal")], EventActor.System, At);

        // Assert
        result.IsSuccess.Should().BeTrue();
        Moves(workflow).Should().BeEquivalentTo([("Proposed", "Notable"), ("Proposed", "Terminal")]);
        workflow.Transitions.Should().Contain(kept, "a kept move keeps its id, so anything attached to it survives");

        var added = workflow.Transitions.Single(t => t.ToStatusId == StatusId(workflow, "Terminal"));
        var raised = workflow.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<WorkflowTransitionsChangedEvent>().Subject;
        raised.Added.Should().BeEquivalentTo([new WorkflowTransitionValues(added.Id, added.FromStatusId, added.ToStatusId)]);
        raised.Removed.Should().BeEquivalentTo([new WorkflowTransitionValues(dropped.Id, dropped.FromStatusId, dropped.ToStatusId)]);
        raised.ConditionsChanged.Should().BeEmpty();
        raised.Transitions.Select(t => t.TransitionId).Should().BeEquivalentTo([kept.Id, added.Id]);
    }

    [Fact]
    public void SetTransitions_RaisesNothing_WhenTheSetIsUnchanged()
    {
        // Arrange
        var workflow = Saved(RestrictedWidgetWorkflow());

        // Act
        var result = workflow.SetTransitions(
            [Move(workflow, "Notable", "Terminal"), Move(workflow, "Proposed", "Notable")], EventActor.System, At);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workflow.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void SetTransitions_ShouldFail_WhenAStatusIsNotInTheWorkflow()
    {
        // Arrange
        var workflow = WidgetWorkflow();

        // Act
        var result = workflow.SetTransitions([new(StatusId(workflow, "Proposed"), Guid.NewGuid())], EventActor.System, At);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Every transition must be between statuses in this workflow.");
        workflow.Transitions.Should().BeEmpty();
    }

    [Fact]
    public void SetTransitions_ShouldFail_ForAMoveToTheSameStatus()
    {
        // Arrange
        var workflow = WidgetWorkflow();

        // Act
        var result = workflow.SetTransitions([Move(workflow, "Notable", "Notable")], EventActor.System, At);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("'Notable' cannot have a transition to itself.");
    }

    [Fact]
    public void SetTransitions_ShouldFail_WhenAMoveIsListedTwice()
    {
        // Arrange
        var workflow = WidgetWorkflow();

        // Act
        var result = workflow.SetTransitions(
            [Move(workflow, "Proposed", "Notable"), Move(workflow, "Proposed", "Notable")], EventActor.System, At);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The same transition is listed more than once.");
        workflow.Transitions.Should().BeEmpty();
    }

    [Theory]
    [InlineData(StatusWorkflowState.Published)]
    [InlineData(StatusWorkflowState.Archived)]
    public void SetTransitions_ShouldFail_OnceTheWorkflowIsNoLongerADraft(StatusWorkflowState state)
    {
        // Arrange
        var workflow = InState(RestrictedWidgetWorkflow(), state);

        // Act
        var result = workflow.SetTransitions([Move(workflow, "Proposed", "Terminal")], EventActor.System, At);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only draft workflows can be restructured.");
        Moves(workflow).Should().BeEquivalentTo([("Proposed", "Notable"), ("Notable", "Terminal")]);
    }

    [Fact]
    public void SetTransitions_ShouldAddAMoveWithItsConditions_AndRecordThem()
    {
        // Arrange
        var workflow = Saved(RestrictedWidgetWorkflow());

        // Act
        var result = workflow.SetTransitions(
            [
                Move(workflow, "Proposed", "Notable"),
                Move(workflow, "Notable", "Terminal"),
                Move(workflow, "Proposed", "Terminal", TransitionConditionKind.RequiresReason),
            ],
            EventActor.System, At);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var added = workflow.Transitions.Single(t => t.FromStatusId == StatusId(workflow, "Proposed") && t.ToStatusId == StatusId(workflow, "Terminal"));
        added.Conditions.Should().ContainSingle().Which.Kind.Should().Be(TransitionConditionKind.RequiresReason);

        var raised = workflow.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<WorkflowTransitionsChangedEvent>().Subject;
        raised.Added.Should().ContainSingle().Which.Conditions.Should().BeEquivalentTo(
            [new WorkflowTransitionConditionValues(added.Conditions.Single().Id, TransitionConditionKind.RequiresReason)]);
        raised.Transitions.Single(t => t.TransitionId == added.Id).Conditions.Should().ContainSingle();
        raised.ConditionsChanged.Should().BeEmpty();
    }

    [Fact]
    public void SetTransitions_ShouldAddAConditionToAKeptMove_RecordingBothEnds()
    {
        // Arrange
        var workflow = Saved(RestrictedWidgetWorkflow());
        var kept = workflow.Transitions.Single(t => t.FromStatusId == StatusId(workflow, "Notable"));

        // Act
        var result = workflow.SetTransitions(
            [Move(workflow, "Proposed", "Notable"), Move(workflow, "Notable", "Terminal", TransitionConditionKind.RequiresReason)],
            EventActor.System, At);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workflow.Transitions.Should().Contain(kept, "changing a move's conditions keeps the move");
        kept.ConditionKinds().Should().Equal(TransitionConditionKind.RequiresReason);

        var raised = workflow.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<WorkflowTransitionsChangedEvent>().Subject;
        raised.Added.Should().BeEmpty();
        raised.Removed.Should().BeEmpty();
        var changed = raised.ConditionsChanged.Should().ContainSingle().Subject;
        changed.TransitionId.Should().Be(kept.Id);
        changed.PreviousConditions.Should().BeEmpty();
        changed.Conditions.Select(c => c.Kind).Should().Equal(TransitionConditionKind.RequiresReason);
    }

    [Fact]
    public void SetTransitions_ShouldRemoveAConditionFromAKeptMove_RecordingBothEnds()
    {
        // Arrange
        var workflow = RestrictedWidgetWorkflow();
        workflow.SetTransitions(
            [Move(workflow, "Proposed", "Notable"), Move(workflow, "Notable", "Terminal", TransitionConditionKind.RequiresReason)],
            EventActor.System, At);
        Saved(workflow);
        var kept = workflow.Transitions.Single(t => t.FromStatusId == StatusId(workflow, "Notable"));
        var condition = kept.Conditions.Single();

        // Act
        var result = workflow.SetTransitions(
            [Move(workflow, "Proposed", "Notable"), Move(workflow, "Notable", "Terminal")], EventActor.System, At);

        // Assert
        result.IsSuccess.Should().BeTrue();
        kept.Conditions.Should().BeEmpty();

        var changed = workflow.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<WorkflowTransitionsChangedEvent>()
            .Subject.ConditionsChanged.Should().ContainSingle().Subject;
        changed.PreviousConditions.Should().BeEquivalentTo([new WorkflowTransitionConditionValues(condition.Id, TransitionConditionKind.RequiresReason)]);
        changed.Conditions.Should().BeEmpty();
    }

    [Fact]
    public void SetTransitions_ShouldKeepTheIdentityOfAConditionItKept()
    {
        // Arrange
        var workflow = RestrictedWidgetWorkflow();
        workflow.SetTransitions(
            [Move(workflow, "Proposed", "Notable"), Move(workflow, "Notable", "Terminal", TransitionConditionKind.RequiresReason)],
            EventActor.System, At);
        var kept = workflow.Transitions.Single(t => t.FromStatusId == StatusId(workflow, "Notable"));
        var reason = kept.Conditions.Single();

        // Act
        workflow.SetTransitions(
            [
                Move(workflow, "Proposed", "Notable"),
                Move(workflow, "Notable", "Terminal", TransitionConditionKind.RequiresPassedApproval, TransitionConditionKind.RequiresReason),
            ],
            EventActor.System, At);

        // Assert
        kept.Conditions.Should().HaveCount(2).And.Contain(reason);
    }

    [Fact]
    public void SetTransitions_RaisesNothing_WhenTheConditionsAreUnchanged()
    {
        // Arrange
        var workflow = RestrictedWidgetWorkflow();
        workflow.SetTransitions(
            [Move(workflow, "Proposed", "Notable"), Move(workflow, "Notable", "Terminal", TransitionConditionKind.RequiresReason)],
            EventActor.System, At);
        Saved(workflow);

        // Act
        var result = workflow.SetTransitions(
            [Move(workflow, "Notable", "Terminal", TransitionConditionKind.RequiresReason), Move(workflow, "Proposed", "Notable")],
            EventActor.System, At);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workflow.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void SetTransitions_ShouldFail_WhenAConditionIsListedTwice()
    {
        // Arrange
        var workflow = Saved(RestrictedWidgetWorkflow());

        // Act
        var result = workflow.SetTransitions(
            [
                Move(workflow, "Proposed", "Notable"),
                Move(workflow, "Notable", "Terminal", TransitionConditionKind.RequiresReason, TransitionConditionKind.RequiresReason),
            ],
            EventActor.System, At);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The move from 'Notable' to 'Terminal' lists the same condition more than once.");
        workflow.Transitions.Should().OnlyContain(t => t.Conditions.Count == 0);
        workflow.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void SetTransitions_ShouldFail_ForAnUnknownCondition()
    {
        // Arrange
        var workflow = Saved(RestrictedWidgetWorkflow());

        // Act
        var result = workflow.SetTransitions(
            [Move(workflow, "Proposed", "Notable"), Move(workflow, "Notable", "Terminal", (TransitionConditionKind)99)],
            EventActor.System, At);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Unknown transition condition.");
        workflow.Transitions.Should().OnlyContain(t => t.Conditions.Count == 0);
    }

    [Theory]
    [InlineData(StatusWorkflowState.Published)]
    [InlineData(StatusWorkflowState.Archived)]
    public void SetTransitions_ShouldNotChangeConditions_OnceTheWorkflowIsNoLongerADraft(StatusWorkflowState state)
    {
        // Arrange
        var workflow = InState(RestrictedWidgetWorkflow(), state);

        // Act
        var result = workflow.SetTransitions(
            [Move(workflow, "Proposed", "Notable"), Move(workflow, "Notable", "Terminal", TransitionConditionKind.RequiresReason)],
            EventActor.System, At);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only draft workflows can be restructured.");
        workflow.Transitions.Should().OnlyContain(t => t.Conditions.Count == 0);
    }

    [Fact]
    public void RemoveStatus_ShouldRecordTheConditionsOfTheTransitionsItRemoves()
    {
        // Arrange
        var workflow = RestrictedWidgetWorkflow();
        workflow.SetTransitions(
            [Move(workflow, "Proposed", "Notable"), Move(workflow, "Notable", "Terminal", TransitionConditionKind.RequiresReason)],
            EventActor.System, At);
        Saved(workflow);

        // Act
        workflow.RemoveStatus(StatusId(workflow, "Terminal"), EventActor.System, At);

        // Assert
        var raised = workflow.DomainEvents.OfType<WorkflowTransitionsChangedEvent>().Should().ContainSingle().Subject;
        raised.Removed.Should().ContainSingle().Which.Conditions.Select(c => c.Kind).Should().Equal(TransitionConditionKind.RequiresReason);
    }

    [Fact]
    public void RemoveStatus_ShouldRemoveItsTransitions_AndRecordThem()
    {
        // Arrange
        var workflow = RestrictedWidgetWorkflow();
        workflow.SetTransitions(
            [Move(workflow, "Proposed", "Notable"), Move(workflow, "Notable", "Terminal"), Move(workflow, "Proposed", "Terminal")],
            EventActor.System, At);
        Saved(workflow);
        var notable = StatusId(workflow, "Notable");

        // Act
        var result = workflow.RemoveStatus(notable, EventActor.System, At);

        // Assert
        result.IsSuccess.Should().BeTrue();
        Moves(workflow).Should().BeEquivalentTo([("Proposed", "Terminal")]);

        var raised = workflow.DomainEvents.OfType<WorkflowTransitionsChangedEvent>().Should().ContainSingle().Subject;
        raised.Added.Should().BeEmpty();
        raised.Removed.Should().HaveCount(2).And.OnlyContain(t => t.FromStatusId == notable || t.ToStatusId == notable);
        raised.Transitions.Should().ContainSingle();
    }

    [Fact]
    public void RemoveStatus_RaisesNoTransitionChange_WhenTheStatusHadNone()
    {
        // Arrange
        var workflow = Saved(RestrictedWidgetWorkflow());
        workflow.AddStatus("Parked", null, StatusCategory.Removed, StatusWorkflow.NoAlias, EventActor.System, At);
        workflow.ClearDomainEvents();

        // Act
        workflow.RemoveStatus(StatusId(workflow, "Parked"), EventActor.System, At);

        // Assert
        workflow.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<WorkflowStatusRemovedEvent>();
        workflow.Transitions.Should().HaveCount(2);
    }

    [Fact]
    public void Clone_ShouldCopyTheModeAndTransitionsOntoTheCloneStatuses()
    {
        // Arrange
        var workflow = RestrictedWidgetWorkflow();

        // Act
        var clone = workflow.Clone("Our Widget Workflow", null, EventActor.System, At);

        // Assert
        clone.TransitionMode.Should().Be(TransitionMode.Restricted);
        Moves(clone).Should().BeEquivalentTo(Moves(workflow));
        clone.Transitions.Should().OnlyContain(t => t.WorkflowId == clone.Id);

        var cloneStatusIds = clone.Statuses.Select(s => s.Id).ToList();
        clone.Transitions.Should().OnlyContain(t => cloneStatusIds.Contains(t.FromStatusId) && cloneStatusIds.Contains(t.ToStatusId));
        clone.Transitions.Select(t => t.Id).Should().NotIntersectWith(workflow.Transitions.Select(t => t.Id));
    }

    [Fact]
    public void Clone_RaisesCreated_CarryingTheModeAndTransitions()
    {
        // Arrange
        var source = Saved(RestrictedWidgetWorkflow());

        // Act
        var clone = source.Clone("Widget Workflow v2", null, EventActor.System, At);

        // Assert
        clone.SetPrivate(w => w.Key, 8);
        clone.ExecutePostPersistenceActions();

        var created = clone.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<WorkflowCreatedEvent>().Subject;
        created.TransitionMode.Should().Be(TransitionMode.Restricted);
        created.Transitions.Select(t => t.TransitionId).Should().BeEquivalentTo(clone.Transitions.Select(t => t.Id));
    }

    [Fact]
    public void Clone_ShouldCopyEachTransitionsConditions_AsNewConditions()
    {
        // Arrange
        var workflow = RestrictedWidgetWorkflow();
        workflow.SetTransitions(
            [Move(workflow, "Proposed", "Notable"), Move(workflow, "Notable", "Terminal", TransitionConditionKind.RequiresReason)],
            EventActor.System, At);
        var source = workflow.Transitions.Single(t => t.FromStatusId == StatusId(workflow, "Notable"));

        // Act
        var clone = workflow.Clone("Our Widget Workflow", null, EventActor.System, At);

        // Assert
        var copy = clone.Transitions.Single(t => t.FromStatusId == StatusId(clone, "Notable"));
        var condition = copy.Conditions.Should().ContainSingle().Subject;
        condition.Kind.Should().Be(TransitionConditionKind.RequiresReason);
        condition.TransitionId.Should().Be(copy.Id);
        condition.Id.Should().NotBe(source.Conditions.Single().Id);
        clone.Transitions.Single(t => t.FromStatusId == StatusId(clone, "Proposed")).Conditions.Should().BeEmpty();
    }

    [Fact]
    public void Clone_RaisesCreated_CarryingEachTransitionsConditions()
    {
        // Arrange
        var workflow = RestrictedWidgetWorkflow();
        workflow.SetTransitions(
            [Move(workflow, "Proposed", "Notable"), Move(workflow, "Notable", "Terminal", TransitionConditionKind.RequiresReason)],
            EventActor.System, At);
        var source = Saved(workflow);

        // Act
        var clone = source.Clone("Widget Workflow v2", null, EventActor.System, At);

        // Assert
        clone.SetPrivate(w => w.Key, 8);
        clone.ExecutePostPersistenceActions();

        var copy = clone.Transitions.Single(t => t.FromStatusId == StatusId(clone, "Notable"));
        var created = clone.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<WorkflowCreatedEvent>().Subject;
        created.Transitions.Single(t => t.TransitionId == copy.Id).Conditions.Should().BeEquivalentTo(
            [new WorkflowTransitionConditionValues(copy.Conditions.Single().Id, TransitionConditionKind.RequiresReason)]);
    }

    [Fact]
    public void Publish_ShouldSucceed_ForARestrictedWorkflowEveryStatusCanReachAndLeave()
    {
        // Arrange
        var workflow = RestrictedWidgetWorkflow();

        // Act
        var result = workflow.Publish(EventActor.System, At);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        workflow.State.Should().Be(StatusWorkflowState.Published);
    }

    [Fact]
    public void Publish_ShouldFail_WhenAStatusCannotBeReached()
    {
        // Arrange
        var workflow = RestrictedWidgetWorkflow();
        workflow.SetTransitions([Move(workflow, "Proposed", "Notable"), Move(workflow, "Notable", "Proposed")], EventActor.System, At);

        // Act
        var result = workflow.Publish(EventActor.System, At);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("'Terminal' cannot be reached from 'Proposed', the initial status.");
        workflow.State.Should().Be(StatusWorkflowState.Draft);
    }

    [Fact]
    public void Publish_ShouldFail_WhenAnActiveStatusHasNoWayOut()
    {
        // Arrange
        var workflow = RestrictedWidgetWorkflow();
        workflow.SetTransitions([Move(workflow, "Proposed", "Notable"), Move(workflow, "Proposed", "Terminal")], EventActor.System, At);

        // Act
        var result = workflow.Publish(EventActor.System, At);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("'Notable' has no transition out of it, so a record that reaches it could not move on.");
    }

    [Fact]
    public void Publish_ShouldSucceed_ForAnAnyToAnyWorkflowWithNoTransitions()
    {
        // Arrange
        var workflow = WidgetWorkflow();

        // Act
        var result = workflow.Publish(EventActor.System, At);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workflow.TransitionIssues().Should().BeEmpty();
    }

    [Fact]
    public void Publish_ShouldFail_ForARestrictedWorkflowWhoseOwnerTypeCannotRestrictTransitions()
    {
        // Arrange
        WorkflowOwners.Register(Locked);
        var workflow = StatusWorkflow.Create("Locked Workflow", null, Locked.Key, EventActor.System, At).Value;
        workflow.AddStatus("Open", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, At);
        workflow.AddStatus("Closed", null, StatusCategory.Done, StatusWorkflow.NoAlias, EventActor.System, At);
        workflow.SetTransitionMode(TransitionMode.Restricted, EventActor.System, At);
        workflow.SetTransitions([Move(workflow, "Open", "Closed")], EventActor.System, At);

        // Act
        var result = workflow.Publish(EventActor.System, At);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Locked workflows cannot restrict transitions. Allow any move to publish this workflow.");
        workflow.EnforcesTransitions.Should().BeFalse();
        workflow.AllowedFrom(StatusId(workflow, "Closed")).Should().BeNull("an owner type that cannot restrict is never enforced");
    }

    [Fact]
    public void AllowedFrom_ShouldBeTheStatusesWithAMoveIntoTheTarget_WhenRestricted()
    {
        // Arrange
        var workflow = RestrictedWidgetWorkflow();

        // Act
        var allowedFrom = workflow.AllowedFrom(StatusId(workflow, "Terminal"));

        // Assert
        allowedFrom.Should().BeEquivalentTo([StatusId(workflow, "Notable")]);
        workflow.AllowedFrom(StatusId(workflow, "Proposed")).Should().BeEmpty("nothing moves into the initial status");
    }

    [Fact]
    public void AllowedTargets_ShouldBeTheMovesOutOfTheStatus_WhenRestricted()
    {
        // Arrange
        var workflow = RestrictedWidgetWorkflow();

        // Act
        var targets = workflow.AllowedTargets(StatusId(workflow, "Proposed"));

        // Assert
        targets.Select(s => s.Name).Should().Equal("Notable");
    }

    [Fact]
    public void AllowedTargets_ShouldBeEveryOtherStatusInOrder_WhenAnyMoveIsAllowed()
    {
        // Arrange
        var workflow = WidgetWorkflow();

        // Act
        var targets = workflow.AllowedTargets(StatusId(workflow, "Notable"));

        // Assert
        targets.Select(s => s.Name).Should().Equal("Proposed", "Terminal");
    }

    [Fact]
    public void TargetRef_ShouldCarryTheStatusesAMoveIsAllowedFrom()
    {
        // Arrange
        var workflow = RestrictedWidgetWorkflow();
        var terminal = workflow.Statuses.Single(s => s.Name == "Terminal");

        // Act
        var target = workflow.TargetRef(terminal.Id);

        // Assert
        target.Should().NotBeNull();
        target!.StatusId.Should().Be(terminal.Id);
        target.Alias.Should().Be(TerminalAlias);
        target.AllowedFrom.Should().BeEquivalentTo([StatusId(workflow, "Notable")]);
    }

    // Restricted Proposed → Notable → Terminal plus Proposed → Terminal, where only Notable → Terminal
    // needs a reason.
    private static StatusWorkflow WidgetWorkflowWithAReasonIntoTerminal()
    {
        var workflow = RestrictedWidgetWorkflow();
        workflow.SetTransitions(
            [
                Move(workflow, "Proposed", "Notable"),
                Move(workflow, "Notable", "Terminal", TransitionConditionKind.RequiresReason),
                Move(workflow, "Proposed", "Terminal"),
            ],
            EventActor.System, At);

        return workflow;
    }

    [Fact]
    public void TargetRef_ShouldCarryWhatEachMoveIntoTheStatusNeeds_ByTheStatusItMovesFrom()
    {
        // Arrange
        var workflow = WidgetWorkflowWithAReasonIntoTerminal();

        // Act
        var target = workflow.TargetRef(StatusId(workflow, "Terminal"))!;

        // Assert
        target.ConditionsFor(StatusId(workflow, "Notable")).Should().BeEquivalentTo([TransitionConditionKind.RequiresReason]);
        target.ConditionsFor(StatusId(workflow, "Proposed")).Should().BeEmpty();
        target.ConditionsFrom.Keys.Should().BeEquivalentTo([StatusId(workflow, "Notable")]);
        target.MetConditions.Should().BeEmpty();
    }

    [Fact]
    public void TargetRef_ShouldCarryNoConditions_WhenTheWorkflowAllowsAnyMove()
    {
        // Arrange
        var workflow = WidgetWorkflowWithAReasonIntoTerminal();
        workflow.SetTransitionMode(TransitionMode.AnyToAny, EventActor.System, At);

        // Act
        var target = workflow.TargetRef(StatusId(workflow, "Terminal"))!;

        // Assert
        // The conditions are kept for a switch back, but an any-to-any workflow enforces none of them.
        target.ConditionsFrom.Should().BeEmpty();
        workflow.ConditionsFor(StatusId(workflow, "Notable"), StatusId(workflow, "Terminal")).Should().BeEmpty();
    }

    [Fact]
    public void ConditionsFor_ShouldBeWhatTheMoveNeeds()
    {
        // Arrange
        var workflow = WidgetWorkflowWithAReasonIntoTerminal();

        // Act
        var conditions = workflow.ConditionsFor(StatusId(workflow, "Notable"), StatusId(workflow, "Terminal"));

        // Assert
        conditions.Should().Equal(TransitionConditionKind.RequiresReason);
        workflow.ConditionsFor(StatusId(workflow, "Proposed"), StatusId(workflow, "Terminal")).Should().BeEmpty();
        workflow.ConditionsFor(StatusId(workflow, "Terminal"), StatusId(workflow, "Proposed")).Should().BeEmpty("there is no such move");
    }

    [Fact]
    public void WithMetConditions_ShouldAddToWhatWasMet_KeepingEverythingElse()
    {
        // Arrange
        var workflow = WidgetWorkflowWithAReasonIntoTerminal();
        var target = workflow.TargetRef(StatusId(workflow, "Terminal"))!;

        // Act
        var evaluated = target.WithMetConditions([TransitionConditionKind.RequiresPassedApproval]);

        // Assert
        evaluated.MetConditions.Should().BeEquivalentTo([TransitionConditionKind.RequiresPassedApproval]);
        evaluated.StatusId.Should().Be(target.StatusId);
        evaluated.AllowedFrom.Should().BeSameAs(target.AllowedFrom);
        evaluated.ConditionsFrom.Should().BeSameAs(target.ConditionsFrom);
        target.MetConditions.Should().BeEmpty("the original reference is unchanged");
    }

    [Fact]
    public void TargetRef_ShouldAllowAnyMove_WhenTheWorkflowAllowsAny()
    {
        // Arrange
        var workflow = WidgetWorkflow();

        // Act
        var target = workflow.TargetRef(StatusId(workflow, "Terminal"));

        // Assert
        target!.AllowedFrom.Should().BeNull();
    }

    [Fact]
    public void TargetRef_ShouldBeNull_ForAStatusOutsideTheWorkflow()
    {
        // Arrange
        var workflow = RestrictedWidgetWorkflow();

        // Act
        var target = workflow.TargetRef(Guid.NewGuid());

        // Assert
        target.Should().BeNull();
    }

    #endregion Transitions

    #region Declared moves

    // A restricted gadget workflow allowing exactly the required moves: Planned → Ready, Ready → Shipped
    // and Ready → Pulled. Leaves out the optional Planned → Shipped and Shipped → Pulled.
    private static StatusWorkflow RestrictedGadgetWorkflow()
    {
        var workflow = StatusWorkflow.Create("Gadget Workflow", null, Gadget.Key, EventActor.System, At).Value;
        workflow.AddStatus("Planned", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, At);
        workflow.AddStatus("Ready", null, StatusCategory.Active, ReadyAlias, EventActor.System, At);
        workflow.AddStatus("Shipped", null, StatusCategory.Done, ShippedAlias, EventActor.System, At);
        workflow.AddStatus("Pulled", null, StatusCategory.Removed, PulledAlias, EventActor.System, At);
        workflow.SetTransitionMode(TransitionMode.Restricted, EventActor.System, At);
        workflow.SetTransitions(
            [Move(workflow, "Planned", "Ready"), Move(workflow, "Ready", "Shipped"), Move(workflow, "Ready", "Pulled")],
            EventActor.System, At).IsSuccess.Should().BeTrue();

        return workflow;
    }

    [Fact]
    public void Publish_ShouldSucceed_ForADeclaringOwnerType_WithEveryRequiredMoveAndNoOptionalOne()
    {
        // Arrange
        var workflow = RestrictedGadgetWorkflow();

        // Act
        var result = workflow.Publish(EventActor.System, At);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        workflow.AllowedFrom(StatusId(workflow, "Pulled")).Should().BeEquivalentTo(
            [StatusId(workflow, "Ready")], "a shipped gadget cannot be pulled under this workflow");
    }

    [Fact]
    public void Publish_ShouldFail_NamingTheRequiredMoveTheWorkflowDoesNotAllow()
    {
        // Arrange
        var workflow = RestrictedGadgetWorkflow();
        workflow.SetTransitions([Move(workflow, "Planned", "Ready"), Move(workflow, "Ready", "Pulled")], EventActor.System, At);

        // Act
        var result = workflow.Publish(EventActor.System, At);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A restricted Gadget workflow must allow 'Ready' to 'Shipped', which Ship makes.");
        workflow.State.Should().Be(StatusWorkflowState.Draft);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Publish_ShouldRefuse_AStatusNeitherInitialNorAliased_ForADeclaringOwnerType(bool withTransitions)
    {
        // Arrange
        // A reassignment can map gadgets into Parked, and no action resolves it to move them out — even
        // along transitions the grid allows, which no action makes.
        var workflow = RestrictedGadgetWorkflow();
        workflow.AddStatus("Parked", null, StatusCategory.Active, StatusWorkflow.NoAlias, EventActor.System, At);
        if (withTransitions)
        {
            workflow.SetTransitions(
                [Move(workflow, "Planned", "Ready"), Move(workflow, "Ready", "Shipped"), Move(workflow, "Ready", "Pulled"),
                    Move(workflow, "Planned", "Parked"), Move(workflow, "Parked", "Ready")],
                EventActor.System, At).IsSuccess.Should().BeTrue();
        }

        // Act
        var result = workflow.Publish(EventActor.System, At);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(
            "'Parked' is neither the initial status nor an aliased one, so no Gadget action can move a record out of it. Remove it, give it an alias, or allow any move.");
        workflow.State.Should().Be(StatusWorkflowState.Draft);
    }

    [Fact]
    public void Publish_ShouldRefuse_AStatusWithAnAliasTheOwnerTypeDoesNotDeclare()
    {
        // Arrange
        // Reachable only through the API, which takes any alias number. Even a final status is refused:
        // a record mapped into it could not be withdrawn or pulled.
        var workflow = RestrictedGadgetWorkflow();
        workflow.AddStatus("Archived", null, StatusCategory.Done, 99, EventActor.System, At);

        // Act
        var result = workflow.Publish(EventActor.System, At);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(
            "'Archived' carries an alias Gadget records do not have, so no Gadget action can move a record out of it. Give it one of theirs, remove it, or allow any move.");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Publish_ShouldRefuse_AnUnfinishedStatusWithNoDeclaredMoveOut(bool undeclaredMoveOut, bool declaredMoveOut)
    {
        // Arrange
        // Shipped made Active: its only declared move out, Pull, is optional. A move no action makes
        // (Shipped → Ready) does not count as a way out.
        var workflow = RestrictedGadgetWorkflow();
        workflow.ReclassifyStatus(StatusId(workflow, "Shipped"), StatusCategory.Active, ShippedAlias, EventActor.System, At);
        List<WorkflowTransitionDefinition> moves =
            [Move(workflow, "Planned", "Ready"), Move(workflow, "Ready", "Shipped"), Move(workflow, "Ready", "Pulled")];
        if (undeclaredMoveOut) moves.Add(Move(workflow, "Shipped", "Ready"));
        if (declaredMoveOut) moves.Add(Move(workflow, "Shipped", "Pulled"));
        workflow.SetTransitions(moves, EventActor.System, At).IsSuccess.Should().BeTrue();

        // Act
        var result = workflow.Publish(EventActor.System, At);

        // Assert
        if (declaredMoveOut)
        {
            result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        }
        else
        {
            result.IsFailure.Should().BeTrue();
            result.Error.Should().Be(
                "'Shipped' has no move out of it that a Gadget action makes, so a record in it could not move on. Allow 'Shipped' to 'Pulled'.");
        }
    }

    [Theory]
    [InlineData("Ready", "Shipped")]
    [InlineData("Planned", "Pulled")]
    public void SetTransitions_ShouldRefuse_AConditionTheMoveDoesNotAccept(string from, string to)
    {
        // Arrange
        var workflow = Saved(RestrictedGadgetWorkflow());
        var conditioned = Move(workflow, from, to, TransitionConditionKind.RequiresReason);
        var others = new[] { Move(workflow, "Planned", "Ready"), Move(workflow, "Ready", "Shipped"), Move(workflow, "Ready", "Pulled") }
            .Where(m => m.FromStatusId != conditioned.FromStatusId || m.ToStatusId != conditioned.ToStatusId);

        // Act
        var result = workflow.SetTransitions([.. others, conditioned], EventActor.System, At);

        // Assert
        // Ready → Shipped is made by a method that takes no reason, and Planned → Pulled by no method.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be($"The move from '{from}' to '{to}' cannot require a reason in a Gadget workflow.");
        workflow.Transitions.SelectMany(t => t.Conditions).Should().BeEmpty();
        workflow.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void SetTransitions_ShouldAccept_AConditionTheMoveAccepts()
    {
        // Arrange
        var workflow = RestrictedGadgetWorkflow();

        // Act
        var result = workflow.SetTransitions(
            [Move(workflow, "Planned", "Ready"), Move(workflow, "Ready", "Shipped"), Move(workflow, "Ready", "Pulled"),
                Move(workflow, "Shipped", "Pulled", TransitionConditionKind.RequiresReason)],
            EventActor.System, At);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        workflow.ConditionsFor(StatusId(workflow, "Shipped"), StatusId(workflow, "Pulled"))
            .Should().Equal(TransitionConditionKind.RequiresReason);
    }

    [Fact]
    public void Publish_ShouldFail_WhenATransitionHoldsAConditionItsMoveNoLongerAccepts()
    {
        // Arrange
        var workflow = RestrictedGadgetWorkflow();
        workflow.SetTransitions(
            [Move(workflow, "Planned", "Ready"), Move(workflow, "Ready", "Shipped"), Move(workflow, "Ready", "Pulled"),
                Move(workflow, "Shipped", "Pulled", TransitionConditionKind.RequiresReason)],
            EventActor.System, At);

        // Moving the Pulled alias to a new status leaves Shipped → Pulled a move no method makes, still
        // holding the condition it was given when it was one.
        workflow.ReclassifyStatus(StatusId(workflow, "Pulled"), StatusCategory.Removed, StatusWorkflow.NoAlias, EventActor.System, At);
        workflow.AddStatus("Scrapped", null, StatusCategory.Removed, PulledAlias, EventActor.System, At);
        workflow.SetTransitions(
            [Move(workflow, "Planned", "Ready"), Move(workflow, "Ready", "Shipped"), Move(workflow, "Ready", "Scrapped"),
                Move(workflow, "Shipped", "Pulled", TransitionConditionKind.RequiresReason)],
            EventActor.System, At).IsSuccess.Should().BeTrue("a condition already held never blocks editing");

        // Act
        var result = workflow.Publish(EventActor.System, At);

        // Assert
        // Pulled, which lost its alias, is reported as well.
        result.IsFailure.Should().BeTrue();
        workflow.TransitionIssues().Should().BeEquivalentTo(
        [
            "'Pulled' is neither the initial status nor an aliased one, so no Gadget action can move a record out of it. Remove it, give it an alias, or allow any move.",
            "'Shipped' to 'Pulled' cannot require a reason in a Gadget workflow.",
        ]);
    }

    [Fact]
    public void DeclaredMoves_ShouldLocateEachMove_SkippingOneThatDoesNotMove_AndMergingOnesThatShareAPair()
    {
        // Arrange
        // Ready is the initial status, so Prepare (initial → Ready) does not move, and Dispatch
        // (initial → Shipped) lands on Ship's pair.
        var workflow = StatusWorkflow.Create("Gadget Workflow", null, Gadget.Key, EventActor.System, At).Value;
        workflow.AddStatus("Ready", null, StatusCategory.Proposed, ReadyAlias, EventActor.System, At);
        workflow.AddStatus("Shipped", null, StatusCategory.Done, ShippedAlias, EventActor.System, At);
        workflow.AddStatus("Pulled", null, StatusCategory.Removed, PulledAlias, EventActor.System, At);
        var (ready, shipped, pulled) = (StatusId(workflow, "Ready"), StatusId(workflow, "Shipped"), StatusId(workflow, "Pulled"));

        // Act
        var moves = workflow.DeclaredMoves();

        // Assert
        moves.Select(m => (m.FromStatusId, m.ToStatusId, m.IsRequired, m.DescribeActions())).Should().BeEquivalentTo(
        [
            (ready, shipped, true, "Ship, Dispatch"),
            (ready, pulled, true, "Pull"),
            (shipped, pulled, false, "Pull"),
        ]);
        moves.Single(m => m.FromStatusId == ready && m.ToStatusId == shipped).AcceptedConditions.Should().BeEmpty();
        moves.Single(m => m.FromStatusId == shipped).AcceptedConditions.Should().BeEquivalentTo([TransitionConditionKind.RequiresReason]);
    }

    [Fact]
    public void AcceptedConditions_ShouldBeEveryKind_ForAnOwnerTypeThatDeclaresNoMoves()
    {
        // Arrange
        var workflow = RestrictedWidgetWorkflow();

        // Act
        var accepted = workflow.AcceptedConditions(StatusId(workflow, "Proposed"), StatusId(workflow, "Terminal"));

        // Assert
        accepted.Should().BeEquivalentTo(Enum.GetValues<TransitionConditionKind>());
    }

    #endregion Declared moves
}
