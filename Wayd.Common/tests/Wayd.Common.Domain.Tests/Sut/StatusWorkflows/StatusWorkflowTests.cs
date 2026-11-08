using CSharpFunctionalExtensions;
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
        new Dictionary<int, WorkflowAlias> { [NotableAlias] = new("Notable", StatusCategory.Active), [TerminalAlias] = new("Terminal", StatusCategory.Done) },
        [NotableAlias, TerminalAlias],
        supportsRestrictedTransitions: true);

    // An owner type whose records cannot be held to a restricted transition set.
    private static readonly WorkflowOwnerDescriptor Locked = new("test.locked", "Locked", new Dictionary<int, WorkflowAlias>(), []);

    private const int ReadyAlias = 21;
    private const int ShippedAlias = 22;
    private const int PulledAlias = 23;
    private const int HeldAlias = 24;

    // An owner type that moves only through named methods and declares the moves they make, as delivery
    // records do. Only pulling a shipped gadget accepts a condition. Held is optional, and so is its only
    // move out.
    private static readonly WorkflowOwnerDescriptor Gadget = new(
        "test.gadget",
        "Gadget",
        new Dictionary<int, WorkflowAlias> { [ReadyAlias] = new("Ready", StatusCategory.Active), [ShippedAlias] = new("Shipped", StatusCategory.Done), [PulledAlias] = new("Pulled", StatusCategory.Removed), [HeldAlias] = new("Held", StatusCategory.Active) },
        [ReadyAlias, ShippedAlias, PulledAlias],
        supportsRestrictedTransitions: true,
        moves:
        [
            WorkflowMove.Required("Prepare", fromAlias: null, ReadyAlias),
            WorkflowMove.Required("Ship", ReadyAlias, ShippedAlias),
            WorkflowMove.Optional("Dispatch", fromAlias: null, ShippedAlias),
            WorkflowMove.Required("Pull", ReadyAlias, PulledAlias),
            WorkflowMove.Optional("Pull", ShippedAlias, PulledAlias, TransitionConditionKind.RequiresReason),
            WorkflowMove.Optional("Release", HeldAlias, ShippedAlias),
        ]);

    // An owner type that requires categories as well as an alias.
    private static readonly WorkflowOwnerDescriptor Staged = new(
        "test.staged",
        "Staged",
        new Dictionary<int, WorkflowAlias> { [NotableAlias] = new("Notable", StatusCategory.Active) },
        [NotableAlias],
        [StatusCategory.Done, StatusCategory.Proposed, StatusCategory.Active]);

    // An owner type whose workflows a sync keeps, requiring categories and an alias a source never supplies.
    private static readonly WorkflowOwnerDescriptor Synced = new(
        "test.synced",
        "Synced",
        new Dictionary<int, WorkflowAlias> { [NotableAlias] = new("Notable", StatusCategory.Active) },
        [NotableAlias],
        [StatusCategory.Proposed, StatusCategory.Active, StatusCategory.Done],
        supportsRestrictedTransitions: true,
        hasManagedWorkflows: true);

    public StatusWorkflowTests() => WorkflowOwners.Register(Widget, Gadget, Staged, Synced);

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

    [Fact]
    public void AddStatus_ShouldFail_WhenTheAliasBelongsUnderAnotherCategory()
    {
        // Arrange
        var workflow = StatusWorkflow.Create("Widget Workflow", null, Widget.Key, EventActor.System, At).Value;

        // Act
        var result = workflow.AddStatus("Terminal", null, StatusCategory.Proposed, TerminalAlias, EventActor.System, At);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("'Terminal' belongs under Done, not Proposed.");
        workflow.Statuses.Should().BeEmpty();
    }

    [Fact]
    public void AddStatus_ShouldFail_ForAnAliasTheOwnerTypeDoesNotDeclare()
    {
        // Arrange — the API takes any number, including one retired from the vocabulary
        var workflow = StatusWorkflow.Create("Widget Workflow", null, Widget.Key, EventActor.System, At).Value;

        // Act
        var result = workflow.AddStatus("Archived", null, StatusCategory.Done, 99, EventActor.System, At);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Widget statuses have no meaning numbered 99.");
        workflow.Statuses.Should().BeEmpty();
    }

    [Fact]
    public void AddStatus_ShouldAcceptAnyCategory_ForAStatusWithNoAlias()
    {
        // Arrange
        var workflow = StatusWorkflow.Create("Widget Workflow", null, Widget.Key, EventActor.System, At).Value;

        // Act
        var result = workflow.AddStatus("Abandoned", null, StatusCategory.Removed, StatusWorkflow.NoAlias, EventActor.System, At);

        // Assert
        result.IsSuccess.Should().BeTrue();
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

    [Fact]
    public void Publish_ShouldFail_WhenARequiredCategoryIsMissing()
    {
        // Arrange
        var workflow = StatusWorkflow.Create("Staged Workflow", null, Staged.Key, EventActor.System, At).Value;
        workflow.AddStatus("Proposed", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, At);
        workflow.AddStatus("Notable", null, StatusCategory.Active, NotableAlias, EventActor.System, At);

        // Act
        var result = workflow.Publish(EventActor.System, At);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A Staged workflow needs a status in each of these categories: Done.");
        workflow.State.Should().Be(StatusWorkflowState.Draft);
    }

    [Fact]
    public void Publish_ShouldFail_NamingEveryMissingCategoryAndAliasAtOnce()
    {
        // Arrange
        var workflow = StatusWorkflow.Create("Staged Workflow", null, Staged.Key, EventActor.System, At).Value;
        workflow.AddStatus("Wrapped Up", null, StatusCategory.Done, StatusWorkflow.NoAlias, EventActor.System, At);

        // Act
        var result = workflow.Publish(EventActor.System, At);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(
            "A Staged workflow needs a status in each of these categories: Proposed, Active. A Staged workflow needs a status for each of: Notable.");
    }

    [Fact]
    public void Publish_ShouldSucceed_WhenEveryRequiredCategoryAndAliasIsPresent()
    {
        // Arrange
        var workflow = StatusWorkflow.Create("Staged Workflow", null, Staged.Key, EventActor.System, At).Value;
        workflow.AddStatus("Proposed", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, At);
        workflow.AddStatus("Notable", null, StatusCategory.Active, NotableAlias, EventActor.System, At);
        workflow.AddStatus("Wrapped Up", null, StatusCategory.Done, StatusWorkflow.NoAlias, EventActor.System, At);

        // Act
        var result = workflow.Publish(EventActor.System, At);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workflow.MissingRequiredCategories().Should().BeEmpty();
    }

    [Fact]
    public void MissingRequiredCategories_ShouldListThem_InCategoryOrder()
    {
        // Arrange
        var workflow = StatusWorkflow.Create("Staged Workflow", null, Staged.Key, EventActor.System, At).Value;
        workflow.AddStatus("Notable", null, StatusCategory.Active, NotableAlias, EventActor.System, At);

        // Act
        var missing = workflow.MissingRequiredCategories();

        // Assert
        missing.Should().Equal(StatusCategory.Proposed, StatusCategory.Done);
    }

    [Fact]
    public void MissingRequiredCategories_ShouldBeEmpty_ForAnOwnerTypeThatRequiresNone()
    {
        // Arrange
        var workflow = StatusWorkflow.Create("Widget Workflow", null, Widget.Key, EventActor.System, At).Value;

        // Act
        var missing = workflow.MissingRequiredCategories();

        // Assert
        missing.Should().BeEmpty();
    }

    [Fact]
    public void PublishSystem_ShouldFail_WhenAStatusFilesItsAliasUnderAnotherCategory()
    {
        // Arrange
        // A seeder adds statuses without the editor's check, as a clone copies them; publishing is what
        // catches the pairing.
        var workflow = StatusWorkflow.CreateSystem("Widget Workflow", null, Widget.Key, EventActor.System, At).Value;
        workflow.AddSystemStatus("Proposed", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, At);
        workflow.AddSystemStatus("Notable", null, StatusCategory.Active, NotableAlias, EventActor.System, At);
        workflow.AddSystemStatus("Closed", null, StatusCategory.Removed, TerminalAlias, EventActor.System, At);

        // Act
        var result = workflow.PublishSystem(EventActor.System, At);

        // Assert
        result.IsFailure.Should().BeTrue();
        const string expected = "'Closed' means Terminal, which belongs under Done, not Removed. Move it to Done or clear its meaning.";
        result.Error.Should().Be(expected);
        workflow.MisfiledAliases().Should().Equal(expected);
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
    public void ReclassifyStatus_ShouldFail_WhenTheAliasBelongsUnderAnotherCategory()
    {
        // Arrange
        var workflow = WidgetWorkflow();
        var terminal = workflow.Statuses.Single(s => s.Name == "Terminal");

        // Act
        var result = workflow.ReclassifyStatus(terminal.Id, StatusCategory.Removed, TerminalAlias, EventActor.System, At);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("'Terminal' belongs under Done, not Removed.");
        workflow.Statuses.Single(s => s.Id == terminal.Id).Category.Should().Be(StatusCategory.Done);
        workflow.DomainEvents.Should().NotContain(e => e is WorkflowStatusReclassifiedEvent);
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
        var result = workflow.ReclassifyStatus(terminal.Id, StatusCategory.Done, TerminalAlias, EventActor.System, At);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workflow.Statuses.Single(s => s.Id == terminal.Id).Alias.Should().Be(TerminalAlias);
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
        // The editor refuses an undeclared alias, so only a clone of an older workflow can hold one. Even a
        // final status is refused: a record mapped into it could not be withdrawn or pulled.
        var seeded = StatusWorkflow.CreateSystem("Seeded Gadget", null, Gadget.Key, EventActor.System, At).Value;
        seeded.AddSystemStatus("Planned", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, At);
        seeded.AddSystemStatus("Ready", null, StatusCategory.Active, ReadyAlias, EventActor.System, At);
        seeded.AddSystemStatus("Shipped", null, StatusCategory.Done, ShippedAlias, EventActor.System, At);
        seeded.AddSystemStatus("Pulled", null, StatusCategory.Removed, PulledAlias, EventActor.System, At);
        seeded.AddSystemStatus("Archived", null, StatusCategory.Done, 99, EventActor.System, At);
        var workflow = seeded.Clone("Gadget Workflow", null, EventActor.System, At);
        workflow.SetTransitionMode(TransitionMode.Restricted, EventActor.System, At);
        workflow.SetTransitions(
            [Move(workflow, "Planned", "Ready"), Move(workflow, "Ready", "Shipped"), Move(workflow, "Ready", "Pulled")],
            EventActor.System, At).IsSuccess.Should().BeTrue();

        // Act
        var result = workflow.Publish(EventActor.System, At);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("'Archived' carries a meaning Gadget statuses do not have. Clear its meaning or remove it.");
        workflow.TransitionIssues().Should().Contain(
            "'Archived' carries an alias Gadget records do not have, so no Gadget action can move a record out of it. Give it one of theirs, remove it, or allow any move.");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Publish_ShouldRefuse_AnUnfinishedStatusWithNoDeclaredMoveOut(bool undeclaredMoveOut, bool declaredMoveOut)
    {
        // Arrange
        // Held's only declared move out, Release, is optional. A move no action makes (Held → Ready) does
        // not count as a way out.
        var workflow = RestrictedGadgetWorkflow();
        workflow.AddStatus("Held", null, StatusCategory.Active, HeldAlias, EventActor.System, At);
        List<WorkflowTransitionDefinition> moves =
            [Move(workflow, "Planned", "Ready"), Move(workflow, "Ready", "Shipped"), Move(workflow, "Ready", "Pulled")];
        if (undeclaredMoveOut) moves.Add(Move(workflow, "Held", "Ready"));
        if (declaredMoveOut) moves.Add(Move(workflow, "Held", "Shipped"));
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
                "'Held' has no move out of it that a Gadget action makes, so a record in it could not move on. Allow 'Held' to 'Shipped'.");
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
        // With no Proposed status, Ready is the initial status as the first, so Prepare (initial → Ready)
        // does not move, and Dispatch (initial → Shipped) lands on Ship's pair.
        var workflow = StatusWorkflow.Create("Gadget Workflow", null, Gadget.Key, EventActor.System, At).Value;
        workflow.AddStatus("Ready", null, StatusCategory.Active, ReadyAlias, EventActor.System, At);
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

    #region LandingStatusFor

    [Fact]
    public void LandingStatusFor_ShouldPreferTheStatusWithTheSameAlias()
    {
        // Arrange
        var workflow = WidgetWorkflow();
        workflow.AddStatus("Also Active", null, StatusCategory.Active, StatusWorkflow.NoAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));
        workflow.ReorderStatuses([.. workflow.Statuses.OrderByDescending(s => s.Name == "Also Active").Select(s => s.Id)], EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var landing = workflow.LandingStatusFor(NotableAlias, StatusCategory.Active);

        // Assert
        // "Also Active" is first in the category, but the alias says what the record means.
        landing!.Name.Should().Be("Notable");
    }

    [Fact]
    public void LandingStatusFor_ShouldFallBackToTheFirstStatusOfTheCategory()
    {
        // Arrange
        var workflow = WidgetWorkflow();
        workflow.AddStatus("Wrapped", null, StatusCategory.Done, StatusWorkflow.NoAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var landing = workflow.LandingStatusFor(StatusWorkflow.NoAlias, StatusCategory.Done);

        // Assert
        landing!.Name.Should().Be("Terminal");
    }

    [Fact]
    public void LandingStatusFor_ShouldBeNull_WhenNoStatusMatchesTheAliasOrCategory()
    {
        // Arrange
        var workflow = WidgetWorkflow();

        // Act
        var landing = workflow.LandingStatusFor(StatusWorkflow.NoAlias, StatusCategory.Removed);

        // Assert
        landing.Should().BeNull();
    }

    #endregion LandingStatusFor

    #region Managed

    private static readonly SourceStatus SourceNew = new("new", "New", StatusCategory.Proposed);
    private static readonly SourceStatus SourceActive = new("active", "Active", StatusCategory.Active);
    private static readonly SourceStatus SourceClosed = new("closed", "Closed", StatusCategory.Done);

    /// <summary>A managed workflow as a sync first creates it, saved once so its events carry a key.</summary>
    private static StatusWorkflow ManagedWorkflow(params SourceStatus[] statuses)
    {
        var workflow = StatusWorkflow.CreateManaged(
            "Agile: Bug", Synced.Key, "conn-1:agile:bug", statuses.Length > 0 ? statuses : [SourceNew, SourceActive, SourceClosed], EventActor.Sync(null), At).Value;
        workflow.SetPrivate(w => w.Key, 9);
        workflow.ExecutePostPersistenceActions();
        workflow.ClearDomainEvents();

        return workflow;
    }

    private static WorkflowStatus StatusKeyed(StatusWorkflow workflow, string externalKey) =>
        workflow.Statuses.Single(s => s.ExternalKey == externalKey);

    [Fact]
    public void CreateManaged_ShouldStartEachStatus_WithTheDescriptionItIsGiven()
    {
        // Act
        var workflow = StatusWorkflow.CreateManaged(
            "Agile: Bug", Synced.Key, "conn-1:agile:bug",
            [SourceNew with { Description = "Not started yet." }, SourceActive, SourceClosed], EventActor.Sync(null), At).Value;

        // Assert
        workflow.Statuses.Select(s => (s.ExternalKey, s.Description)).Should().Equal(
            ("new", "Not started yet."), ("active", null), ("closed", null));
    }

    [Fact]
    public void CreateManaged_ShouldPublishTheSourcesStatusesInItsOrder()
    {
        // Act
        var result = StatusWorkflow.CreateManaged("Agile: Bug", Synced.Key, " conn-1:agile:bug ", [SourceClosed, SourceNew], EventActor.Sync(null), At);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var workflow = result.Value;
        workflow.Ownership.Should().Be(WorkflowOwnership.Managed);
        workflow.ExternalKey.Should().Be("conn-1:agile:bug");
        workflow.State.Should().Be(StatusWorkflowState.Published);
        workflow.Statuses.Select(s => (s.ExternalKey, s.Name, s.Category, s.Alias, s.Order)).Should().Equal(
            ("closed", "Closed", StatusCategory.Done, StatusWorkflow.NoAlias, 1),
            ("new", "New", StatusCategory.Proposed, StatusWorkflow.NoAlias, 2));
    }

    [Fact]
    public void CreateManaged_ShouldRecordTheSourceKeysInItsCreation()
    {
        // Arrange
        var workflow = StatusWorkflow.CreateManaged("Agile: Bug", Synced.Key, "conn-1:agile:bug", [SourceNew], EventActor.Sync(null), At).Value;

        // Act
        workflow.SetPrivate(w => w.Key, 9);
        workflow.ExecutePostPersistenceActions();

        // Assert
        var created = workflow.DomainEvents.OfType<WorkflowCreatedEvent>().Should().ContainSingle().Subject;
        created.ExternalKey.Should().Be("conn-1:agile:bug");
        created.Statuses.Should().ContainSingle().Which.ExternalKey.Should().Be("new");
        workflow.DomainEvents.OfType<WorkflowPublishedEventV2>().Should().ContainSingle();
    }

    [Fact]
    public void CreateManaged_ShouldSucceed_WhenTheSourceLacksARequiredCategory()
    {
        // Act
        var result = StatusWorkflow.CreateManaged("Agile: Bug", Synced.Key, "conn-1:agile:bug", [SourceNew, SourceActive], EventActor.Sync(null), At);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.CheckIssues().Should().Contain(i =>
            i.Check == WorkflowCheck.RequiredCategories && i.Severity == WorkflowCheckSeverity.Warned && i.Message.Contains("Done"));
    }

    [Fact]
    public void CreateManaged_ShouldBeRefused_ForAnOwnerTypeWhoseWorkflowsAreNotSynced()
    {
        // Act
        var result = StatusWorkflow.CreateManaged("Widgets", Widget.Key, "conn-1:widgets", [SourceNew], EventActor.Sync(null), At);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Widget workflows are not kept by a sync.");
    }

    [Theory]
    [InlineData("NEW", "Active", "The source lists the status 'new' more than once.")]
    [InlineData("other", "new", "The source has more than one status named 'New'.")]
    [InlineData(" ", "Other", "Every source status needs a key and a name.")]
    public void CreateManaged_ShouldBeRefused_ForAStatusListItCouldNotHold(string secondKey, string secondName, string error)
    {
        // Arrange — keys and names collide without regard to case
        SourceStatus[] statuses = [SourceNew, new(secondKey, secondName, StatusCategory.Active)];

        // Act
        var result = StatusWorkflow.CreateManaged("Agile: Bug", Synced.Key, "conn-1:agile:bug", statuses, EventActor.Sync(null), At);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void SyncFromSource_ShouldAddRenameAndReorder_ByExternalKey()
    {
        // Arrange
        var workflow = ManagedWorkflow();
        var activeId = StatusKeyed(workflow, "active").Id;

        // Act
        var result = workflow.SyncFromSource(
            "Agile: Bug",
            [SourceNew, new("resolved", "Resolved", StatusCategory.Done), new("ACTIVE", "In Progress", StatusCategory.Done), SourceClosed],
            EventActor.Sync(null), At);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        workflow.Statuses.Select(s => (s.ExternalKey, s.Name, s.Order)).Should().Equal(
            ("new", "New", 1), ("resolved", "Resolved", 2), ("active", "In Progress", 3), ("closed", "Closed", 4));
        StatusKeyed(workflow, "active").Id.Should().Be(activeId, "a rename at the source renames the status rather than adding another");
        StatusKeyed(workflow, "active").Category.Should().Be(StatusCategory.Active, "the category is the administrators' mapping once a status exists");

        var added = workflow.DomainEvents.OfType<WorkflowStatusAddedEvent>().Should().ContainSingle().Subject;
        (added.ExternalKey, added.Order, added.Key).Should().Be(("resolved", 2, 9));
        workflow.DomainEvents.OfType<WorkflowStatusRenamedEvent>().Should().ContainSingle()
            .Which.Previous.Name.Should().Be("Active");
        workflow.DomainEvents.OfType<WorkflowStatusesReorderedEvent>().Should().BeEmpty("the statuses already here kept their order");
    }

    [Fact]
    public void SyncFromSource_ShouldKeepEachStatusesDescription_ThroughARenameARetirementAndAReinstatement()
    {
        // Arrange — the description is Wayd's, which no source carries
        var workflow = ManagedWorkflow();
        foreach (var status in workflow.Statuses)
            status.SetPrivate(s => s.Description, $"About {status.ExternalKey}");

        // Act
        var renamedAndRetired = workflow.SyncFromSource("Agile: Bug", [SourceNew, new("active", "In Progress", StatusCategory.Active)], EventActor.Sync(null), At);
        var reinstated = workflow.SyncFromSource("Agile: Bug", [SourceNew, new("active", "In Progress", StatusCategory.Active), SourceClosed], EventActor.Sync(null), At);

        // Assert
        renamedAndRetired.IsSuccess.Should().BeTrue();
        reinstated.IsSuccess.Should().BeTrue();
        workflow.Statuses.Select(s => (s.ExternalKey, s.Description)).Should().BeEquivalentTo(
            [("new", "About new"), ("active", "About active"), ("closed", "About closed")]);
        workflow.DomainEvents.OfType<WorkflowStatusRenamedEvent>().Should().ContainSingle()
            .Which.Description.Should().Be("About active", "the rename event reports the description the status kept");
    }

    [Fact]
    public void SyncFromSource_ShouldGiveANewStatusNoDescription()
    {
        // Arrange
        var workflow = ManagedWorkflow();

        // Act
        workflow.SyncFromSource("Agile: Bug", [SourceNew, SourceActive, SourceClosed, new("resolved", "Resolved", StatusCategory.Done)], EventActor.Sync(null), At);

        // Assert
        StatusKeyed(workflow, "resolved").Description.Should().BeNull();
    }

    [Fact]
    public void SyncFromSource_ShouldStartANewStatus_WithTheDescriptionItIsGiven()
    {
        // Arrange
        var workflow = ManagedWorkflow();

        // Act
        workflow.SyncFromSource("Agile: Bug",
            [SourceNew, SourceActive, SourceClosed, new("resolved", "Resolved", StatusCategory.Done, Description: "  Fixed, awaiting checks.  ")],
            EventActor.Sync(null), At);

        // Assert
        StatusKeyed(workflow, "resolved").Description.Should().Be("Fixed, awaiting checks.");
        workflow.DomainEvents.OfType<WorkflowStatusAddedEvent>().Should().ContainSingle()
            .Which.Description.Should().Be("Fixed, awaiting checks.", "the Activity log records it with the status");
        workflow.DomainEvents.OfType<WorkflowStatusDescriptionChangedEvent>().Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Ours.")]
    public void SyncFromSource_ShouldIgnoreTheDescriptionItIsGiven_ForAStatusAlreadyThere(string? description)
    {
        // Arrange — described, then rewritten or cleared; a cleared description stays cleared
        var workflow = ManagedWorkflow();
        var active = StatusKeyed(workflow, "active");
        workflow.DescribeStatus(active.Id, "From the sync.", EventActor.Sync(null), At);
        workflow.DescribeStatus(active.Id, description, EventActor.User("admin"), At);
        workflow.ClearDomainEvents();

        // Act
        var result = workflow.SyncFromSource("Agile: Bug",
            [SourceNew, SourceActive with { Description = "Being worked on." }, SourceClosed], EventActor.Sync(null), At);

        // Assert
        result.IsSuccess.Should().BeTrue();
        active.Description.Should().Be(description);
        workflow.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void SyncFromSource_ShouldRaiseNothing_WhenTheSourceIsUnchanged()
    {
        // Arrange
        var workflow = ManagedWorkflow();

        // Act
        var result = workflow.SyncFromSource("Agile: Bug", [SourceNew, SourceActive, SourceClosed], EventActor.Sync(null), At);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workflow.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void SyncFromSource_ShouldReportAReorder_OfStatusesAlreadyThere()
    {
        // Arrange
        var workflow = ManagedWorkflow();

        // Act
        workflow.SyncFromSource("Agile: Bug", [SourceActive, SourceNew, SourceClosed], EventActor.Sync(null), At);

        // Assert
        workflow.Statuses.Select(s => s.ExternalKey).Should().Equal("active", "new", "closed");
        workflow.DomainEvents.OfType<WorkflowStatusesReorderedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void SyncFromSource_ShouldRetireADroppedStatus_ThatStillResolves()
    {
        // Arrange
        var workflow = ManagedWorkflow();
        var active = StatusKeyed(workflow, "active");
        workflow.ReclassifyStatus(active.Id, StatusCategory.Active, NotableAlias, EventActor.System, At);
        workflow.ClearDomainEvents();

        // Act
        var result = workflow.SyncFromSource("Agile: Bug", [SourceNew, SourceClosed], EventActor.Sync(null), At);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        active.IsRetired.Should().BeTrue();
        workflow.Statuses.Should().Contain(active, "records and history holding it still resolve through the workflow");
        workflow.Statuses.Last().Should().Be(active, "a retired status sits after the active ones");
        workflow.ActiveStatuses.Select(s => s.ExternalKey).Should().Equal("new", "closed");
        workflow.TargetRef(active.Id).Should().BeNull("no record can be given a retired status");
        workflow.AllowedTargets(StatusKeyed(workflow, "new").Id).Should().NotContain(active);
        workflow.StatusFor(NotableAlias).Should().BeNull();
        active.Alias.Should().Be(StatusWorkflow.NoAlias, "the meaning is freed for a status records can still be given");

        workflow.DomainEvents.OfType<WorkflowStatusRetiredEvent>().Should().ContainSingle()
            .Which.Should().Match<WorkflowStatusRetiredEvent>(e => e.StatusId == active.Id && e.ExternalKey == "active");
        workflow.DomainEvents.OfType<WorkflowStatusAliasChangedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void SyncFromSource_ShouldWarn_WhenRetiringLeavesARequiredCategoryEmpty()
    {
        // Arrange
        var workflow = ManagedWorkflow();

        // Act
        workflow.SyncFromSource("Agile: Bug", [SourceNew, SourceActive], EventActor.Sync(null), At);

        // Assert
        workflow.MissingRequiredCategories().Should().Equal(StatusCategory.Done);
        workflow.State.Should().Be(StatusWorkflowState.Published, "a managed workflow keeps syncing");
    }

    [Fact]
    public void SyncFromSource_ShouldReinstateAStatusTheSourceBringsBack()
    {
        // Arrange
        var workflow = ManagedWorkflow();
        var active = StatusKeyed(workflow, "active");
        workflow.SyncFromSource("Agile: Bug", [SourceNew, SourceClosed], EventActor.Sync(null), At);
        workflow.ClearDomainEvents();

        // Act
        workflow.SyncFromSource("Agile: Bug", [SourceNew, SourceActive, SourceClosed], EventActor.Sync(null), At);

        // Assert
        active.IsRetired.Should().BeFalse();
        workflow.Statuses.Should().HaveCount(3, "the status is reinstated, not added again");
        workflow.Statuses.Select(s => s.ExternalKey).Should().Equal("new", "active", "closed");
        workflow.DomainEvents.OfType<WorkflowStatusReinstatedEvent>().Should().ContainSingle().Which.StatusId.Should().Be(active.Id);
        workflow.DomainEvents.OfType<WorkflowStatusAddedEvent>().Should().BeEmpty();
    }

    [Fact]
    public void CreateManaged_ShouldKeepAHiddenStatusRetired_AfterTheOnesTheSourceOffers()
    {
        // Arrange
        var resolved = new SourceStatus("resolved", "Resolved", StatusCategory.Active, IsHidden: true);

        // Act
        var result = StatusWorkflow.CreateManaged("Agile: Bug", Synced.Key, "conn-1:agile:bug", [SourceNew, resolved, SourceClosed], EventActor.Sync(null), At);

        // Assert
        // History may hold a state the source has hidden, so it must resolve, with its category.
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var workflow = result.Value;
        workflow.Statuses.Select(s => (s.ExternalKey, s.Category, s.IsRetired, s.Order)).Should().Equal(
            ("new", StatusCategory.Proposed, false, 1),
            ("closed", StatusCategory.Done, false, 2),
            ("resolved", StatusCategory.Active, true, 3));
        workflow.ActiveStatuses.Select(s => s.ExternalKey).Should().Equal("new", "closed");

        workflow.SetPrivate(w => w.Key, 9);
        workflow.ExecutePostPersistenceActions();
        workflow.DomainEvents.Select(e => e.GetType()).Should().Equal(
            typeof(WorkflowCreatedEvent), typeof(WorkflowPublishedEventV2), typeof(WorkflowStatusRetiredEvent));
        workflow.DomainEvents.OfType<WorkflowStatusRetiredEvent>().Single().ExternalKey.Should().Be("resolved");
    }

    [Fact]
    public void SyncFromSource_ShouldRetireAStatusTheSourceHides_AndKeepItsMapping()
    {
        // Arrange
        var workflow = ManagedWorkflow();
        var active = StatusKeyed(workflow, "active");
        workflow.ReclassifyStatus(active.Id, StatusCategory.Done, StatusWorkflow.NoAlias, EventActor.System, At);
        workflow.ClearDomainEvents();

        // Act
        var result = workflow.SyncFromSource(
            "Agile: Bug", [SourceNew, SourceActive with { Name = "In Progress", IsHidden = true }, SourceClosed], EventActor.Sync(null), At);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        active.IsRetired.Should().BeTrue();
        active.Name.Should().Be("In Progress", "a hidden status still takes the source's name");
        active.Category.Should().Be(StatusCategory.Done, "the administrators' mapping is kept");
        workflow.Statuses.Last().Should().Be(active);
        workflow.DomainEvents.OfType<WorkflowStatusRetiredEvent>().Should().ContainSingle().Which.StatusId.Should().Be(active.Id);
    }

    [Fact]
    public void SyncFromSource_ShouldReinstateAHiddenStatus_WhenTheSourceOffersItAgain()
    {
        // Arrange
        var workflow = ManagedWorkflow();
        var active = StatusKeyed(workflow, "active");
        workflow.SyncFromSource("Agile: Bug", [SourceNew, SourceActive with { IsHidden = true }, SourceClosed], EventActor.Sync(null), At);
        workflow.ClearDomainEvents();

        // Act
        workflow.SyncFromSource("Agile: Bug", [SourceNew, SourceActive, SourceClosed], EventActor.Sync(null), At);

        // Assert
        active.IsRetired.Should().BeFalse();
        workflow.Statuses.Select(s => s.ExternalKey).Should().Equal("new", "active", "closed");
        workflow.DomainEvents.OfType<WorkflowStatusReinstatedEvent>().Should().ContainSingle().Which.StatusId.Should().Be(active.Id);
    }

    [Fact]
    public void SyncFromSource_ShouldAddANewHiddenStatus_AsRetired()
    {
        // Arrange
        var workflow = ManagedWorkflow();
        var resolved = new SourceStatus("resolved", "Resolved", StatusCategory.Active, IsHidden: true);

        // Act
        var result = workflow.SyncFromSource("Agile: Bug", [SourceNew, resolved, SourceActive, SourceClosed], EventActor.Sync(null), At);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var added = StatusKeyed(workflow, "resolved");
        added.IsRetired.Should().BeTrue();
        workflow.Statuses.Last().Should().Be(added);
        workflow.ActiveStatuses.Select(s => s.ExternalKey).Should().Equal("new", "active", "closed");
        workflow.DomainEvents.Select(e => e.GetType()).Should().Equal(typeof(WorkflowStatusAddedEvent), typeof(WorkflowStatusRetiredEvent));
    }

    [Fact]
    public void SyncFromSource_ShouldRaiseNothing_WhenAHiddenStatusStaysHidden()
    {
        // Arrange
        var hidden = SourceActive with { IsHidden = true };
        var workflow = ManagedWorkflow(SourceNew, hidden, SourceClosed);

        // Act
        var result = workflow.SyncFromSource("Agile: Bug", [SourceNew, hidden, SourceClosed], EventActor.Sync(null), At);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        workflow.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void CreateManaged_ShouldBeRefused_WhenTheSourceHidesEveryStatus()
    {
        // Act
        var result = StatusWorkflow.CreateManaged(
            "Agile: Bug", Synced.Key, "conn-1:agile:bug", [SourceNew with { IsHidden = true }, SourceClosed with { IsHidden = true }], EventActor.Sync(null), At);

        // Assert
        // It could never give a record a status.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A managed workflow needs at least one status its source offers.");
    }

    [Fact]
    public void SyncFromSource_ShouldRetireEveryStatus_WhenTheSourceHidesThemAll()
    {
        // Arrange
        var workflow = ManagedWorkflow();

        // Act
        var result = workflow.SyncFromSource(
            "Agile: Bug", [SourceNew with { IsHidden = true }, SourceActive with { IsHidden = true }, SourceClosed with { IsHidden = true }], EventActor.Sync(null), At);

        // Assert
        // Records and history holding them still resolve; none can be given a status any more.
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        workflow.Statuses.Should().HaveCount(3).And.OnlyContain(s => s.IsRetired);
        workflow.ActiveStatuses.Should().BeEmpty();
        workflow.DomainEvents.OfType<WorkflowStatusRetiredEvent>().Should().HaveCount(3);
    }

    [Fact]
    public void SyncFromSource_ShouldPlaceANewHiddenStatus_AfterOnesAlreadyRetired()
    {
        // Arrange
        var workflow = ManagedWorkflow();
        workflow.SyncFromSource("Agile: Bug", [SourceNew, SourceClosed], EventActor.Sync(null), At);
        workflow.ClearDomainEvents();
        var resolved = new SourceStatus("resolved", "Resolved", StatusCategory.Active, IsHidden: true);

        // Act
        workflow.SyncFromSource("Agile: Bug", [SourceNew, resolved, SourceClosed], EventActor.Sync(null), At);

        // Assert
        workflow.Statuses.OrderBy(s => s.Order).Select(s => s.ExternalKey).Should().Equal("new", "closed", "active", "resolved");
    }

    [Fact]
    public void SyncFromSource_ShouldLetANewStatusTakeARetiredOnesName()
    {
        // Arrange
        var workflow = ManagedWorkflow();
        workflow.SyncFromSource("Agile: Bug", [SourceNew, SourceClosed], EventActor.Sync(null), At);

        // Act
        var result = workflow.SyncFromSource("Agile: Bug", [SourceNew, new("active-2", "Active", StatusCategory.Active), SourceClosed], EventActor.Sync(null), At);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        workflow.Statuses.Where(s => s.Name == "Active").Select(s => (s.ExternalKey, s.IsRetired)).Should().BeEquivalentTo(
            [("active", true), ("active-2", false)]);
    }

    [Fact]
    public void SyncFromSource_ShouldTakeTheSourcesName()
    {
        // Arrange
        var workflow = ManagedWorkflow();

        // Act
        workflow.SyncFromSource("Agile: Defect", [SourceNew, SourceActive, SourceClosed], EventActor.Sync(null), At);

        // Assert
        workflow.Name.Should().Be("Agile: Defect");
        workflow.DomainEvents.OfType<WorkflowDetailsUpdatedEvent>().Should().ContainSingle().Which.Previous.Name.Should().Be("Agile: Bug");
    }

    [Fact]
    public void SyncFromSource_ShouldBeRefused_ForAnOwnedWorkflow()
    {
        // Arrange
        var workflow = WidgetWorkflow();
        workflow.ClearDomainEvents();

        // Act
        var result = workflow.SyncFromSource("Widget Workflow", [SourceNew], EventActor.Sync(null), At);

        // Assert
        result.IsFailure.Should().BeTrue();
        workflow.Statuses.Should().HaveCount(3);
        workflow.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void SyncFromSource_ShouldRememberTheNamesASourceRenamedAStatusFrom_OldestFirst()
    {
        // Arrange
        var workflow = ManagedWorkflow();

        // Act
        workflow.SyncFromSource("Agile: Bug", [SourceNew, new("active", "Doing", StatusCategory.Active), SourceClosed], EventActor.Sync(null), At);
        workflow.SyncFromSource("Agile: Bug", [SourceNew, new("active", "In Progress", StatusCategory.Active), SourceClosed], EventActor.Sync(null), At);

        // Assert
        StatusKeyed(workflow, "active").FormerNames.Should().Equal("Active", "Doing");
        StatusKeyed(workflow, "new").FormerNames.Should().BeEmpty("a status the source did not rename has no former name");
    }

    [Fact]
    public void SyncFromSource_ShouldForgetAFormerName_TheStatusTakesBack()
    {
        // Arrange
        var workflow = ManagedWorkflow();
        workflow.SyncFromSource("Agile: Bug", [SourceNew, new("active", "Doing", StatusCategory.Active), SourceClosed], EventActor.Sync(null), At);

        // Act
        workflow.SyncFromSource("Agile: Bug", [SourceNew, SourceActive, SourceClosed], EventActor.Sync(null), At);

        // Assert
        StatusKeyed(workflow, "active").Name.Should().Be("Active");
        StatusKeyed(workflow, "active").FormerNames.Should().Equal("Doing");
    }

    [Fact]
    public void SyncFromSource_ShouldNotRememberAName_WhenOnlyItsCaseChanges()
    {
        // Arrange — sources report states by name without regard to case, so the old casing still matches
        var workflow = ManagedWorkflow();

        // Act
        workflow.SyncFromSource("Agile: Bug", [SourceNew, new("active", "ACTIVE", StatusCategory.Active), SourceClosed], EventActor.Sync(null), At);

        // Assert
        StatusKeyed(workflow, "active").Name.Should().Be("ACTIVE");
        StatusKeyed(workflow, "active").FormerNames.Should().BeEmpty();
    }

    [Fact]
    public void DescribeStatus_ShouldRewriteTheDescription_OfAManagedWorkflowsStatus()
    {
        // Arrange — the source owns the name; the description is Wayd's
        var workflow = ManagedWorkflow();
        var active = StatusKeyed(workflow, "active");

        // Act
        var result = workflow.DescribeStatus(active.Id, "  Being worked on.  ", EventActor.Sync(null), At);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        active.Description.Should().Be("Being worked on.");
        active.Name.Should().Be("Active");
        var changed = workflow.DomainEvents.OfType<WorkflowStatusDescriptionChangedEvent>().Should().ContainSingle().Subject;
        (changed.Id, changed.StatusId, changed.FromDescription, changed.ToDescription).Should().Be((workflow.Id, active.Id, null, "Being worked on."));
        workflow.DomainEvents.Should().ContainSingle("nothing else about the workflow changed");
    }

    [Fact]
    public void DescribeStatus_ShouldReportThePreviousDescription()
    {
        // Arrange
        var workflow = ManagedWorkflow();
        var active = StatusKeyed(workflow, "active");
        workflow.DescribeStatus(active.Id, "Before", EventActor.Sync(null), At);
        workflow.ClearDomainEvents();

        // Act
        var result = workflow.DescribeStatus(active.Id, null, EventActor.Sync(null), At);

        // Assert
        result.IsSuccess.Should().BeTrue();
        active.Description.Should().BeNull();
        var changed = workflow.DomainEvents.OfType<WorkflowStatusDescriptionChangedEvent>().Should().ContainSingle().Subject;
        (changed.FromDescription, changed.ToDescription).Should().Be(("Before", null));
    }

    [Theory]
    [InlineData("Same")]
    [InlineData(" Same ")]
    public void DescribeStatus_ShouldRaiseNothing_WhenTheDescriptionIsUnchanged(string description)
    {
        // Arrange — compared after trimming
        var workflow = ManagedWorkflow();
        var active = StatusKeyed(workflow, "active");
        workflow.DescribeStatus(active.Id, "Same", EventActor.Sync(null), At);
        workflow.ClearDomainEvents();

        // Act
        var result = workflow.DescribeStatus(active.Id, description, EventActor.Sync(null), At);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workflow.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void DescribeStatus_ShouldDescribeARetiredStatus()
    {
        // Arrange — records still hold it, so administrators still read it
        var workflow = ManagedWorkflow();
        workflow.SyncFromSource("Agile: Bug", [SourceNew, SourceClosed], EventActor.Sync(null), At);
        var active = StatusKeyed(workflow, "active");

        // Act
        var result = workflow.DescribeStatus(active.Id, "No longer offered.", EventActor.Sync(null), At);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        active.Description.Should().Be("No longer offered.");
    }

    [Fact]
    public void DescribeStatus_ShouldFail_ForAnUnknownStatus()
    {
        // Arrange
        var workflow = ManagedWorkflow();

        // Act
        var result = workflow.DescribeStatus(Guid.NewGuid(), "Anything", EventActor.Sync(null), At);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Status not found.");
        workflow.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void DescribeStatus_ShouldFail_ForAnArchivedWorkflow()
    {
        // Arrange
        var workflow = InState(WidgetWorkflow(), StatusWorkflowState.Archived);
        workflow.ClearDomainEvents();
        var status = workflow.Statuses.First();

        // Act
        var result = workflow.DescribeStatus(status.Id, "Anything", EventActor.System, At);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("An archived workflow cannot be modified.");
        status.Description.Should().NotBe("Anything");
        workflow.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void DescribeStatus_ShouldRewriteTheDescription_OfAPublishedOwnedWorkflowsStatus()
    {
        // Arrange — allowed whoever keeps the workflow; records hold the status id, not its description
        var workflow = Saved(InState(WidgetWorkflow(), StatusWorkflowState.Published));
        var status = workflow.Statuses.First();

        // Act
        var result = workflow.DescribeStatus(status.Id, "Rewritten.", EventActor.System, At);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        status.Description.Should().Be("Rewritten.");
        workflow.DomainEvents.OfType<WorkflowStatusDescriptionChangedEvent>().Should().ContainSingle()
            .Which.ToDescription.Should().Be("Rewritten.");
    }

    [Fact]
    public void DescribeStatus_ShouldFail_ForASystemWorkflow()
    {
        // Arrange
        var workflow = StatusWorkflow.CreateSystem("Widget Workflow", null, Widget.Key, EventActor.System, At).Value;
        workflow.AddSystemStatus("Draft", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, At);
        workflow.ClearDomainEvents();
        var status = workflow.Statuses.Single();

        // Act
        var result = workflow.DescribeStatus(status.Id, "Anything", EventActor.System, At);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("System workflows cannot be modified");
        status.Description.Should().BeNull();
        workflow.DomainEvents.Should().BeEmpty();
    }

    public static TheoryData<string, Func<StatusWorkflow, Result>> AdministratorStructureEdits() => new()
    {
        { "Update", w => w.Update("Renamed", null, EventActor.System, At) },
        { "AddStatus", w => w.AddStatus("Blocked", null, StatusCategory.Active, StatusWorkflow.NoAlias, EventActor.System, At) },
        { "RemoveStatus", w => w.RemoveStatus(w.Statuses.First().Id, EventActor.System, At) },
        { "RenameStatus", w => w.RenameStatus(w.Statuses.First().Id, "Renamed", null, EventActor.System, At) },
        { "ReorderStatuses", w => w.ReorderStatuses([.. w.Statuses.Reverse().Select(s => s.Id)], EventActor.System, At) },
        { "SetTransitionMode", w => w.SetTransitionMode(TransitionMode.Restricted, EventActor.System, At) },
        { "SetTransitions", w => w.SetTransitions([], EventActor.System, At) },
        { "Publish", w => w.Publish(EventActor.System, At) },
        { "Archive", w => w.Archive(isAssigned: false, EventActor.System, At) },
    };

    [Theory]
    [MemberData(nameof(AdministratorStructureEdits))]
    public void ManagedWorkflow_ShouldRefuseAdministratorsChangingItsStructure(string edit, Func<StatusWorkflow, Result> act)
    {
        // Arrange
        var workflow = ManagedWorkflow();
        var before = workflow.Statuses.Select(s => (s.Id, s.Name, s.Order)).ToList();

        // Act
        var result = act(workflow);

        // Assert
        result.IsFailure.Should().BeTrue($"{edit} would change what the source decides");
        workflow.Statuses.Select(s => (s.Id, s.Name, s.Order)).Should().Equal(before);
        workflow.Name.Should().Be("Agile: Bug");
        workflow.State.Should().Be(StatusWorkflowState.Published);
        workflow.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ReclassifyStatus_ShouldChangeTheCategoryMapping_OfAPublishedManagedWorkflow()
    {
        // Arrange
        var workflow = ManagedWorkflow(SourceNew, new("active", "Active", StatusCategory.Proposed), SourceClosed);
        var active = StatusKeyed(workflow, "active");

        // Act
        var result = workflow.ReclassifyStatus(active.Id, StatusCategory.Active, NotableAlias, EventActor.System, At);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        (active.Category, active.Alias).Should().Be((StatusCategory.Active, NotableAlias));
        workflow.DomainEvents.OfType<WorkflowStatusReclassifiedEvent>().Should().ContainSingle()
            .Which.OwnerType.Should().Be(Synced.Key, "the owning module re-stamps its records from it");
        workflow.CheckIssues().Should().BeEmpty();
    }

    [Fact]
    public void ReclassifyStatus_ShouldBeRefused_ForARetiredStatus()
    {
        // Arrange
        var workflow = ManagedWorkflow();
        var active = StatusKeyed(workflow, "active");
        workflow.SyncFromSource("Agile: Bug", [SourceNew, SourceClosed], EventActor.Sync(null), At);
        workflow.ClearDomainEvents();

        // Act
        var result = workflow.ReclassifyStatus(active.Id, StatusCategory.Done, StatusWorkflow.NoAlias, EventActor.System, At);

        // Assert
        result.IsFailure.Should().BeTrue();
        active.Category.Should().Be(StatusCategory.Active);
        workflow.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void CheckIssues_ShouldWarnAManagedWorkflow_AndEnforceOnAnOwnedOne()
    {
        // Arrange — both lack the required Notable alias
        var managed = ManagedWorkflow();
        var owned = StatusWorkflow.Create("Synced", null, Synced.Key, EventActor.System, At).Value;
        owned.AddStatus("New", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, At);
        owned.AddStatus("Active", null, StatusCategory.Active, StatusWorkflow.NoAlias, EventActor.System, At);
        owned.AddStatus("Closed", null, StatusCategory.Done, StatusWorkflow.NoAlias, EventActor.System, At);

        // Act
        var managedIssues = managed.CheckIssues();
        var ownedIssues = owned.CheckIssues();

        // Assert
        managedIssues.Should().ContainSingle().Which.Should().Be(new WorkflowCheckIssue(
            WorkflowCheck.RequiredAliases, WorkflowCheckSeverity.Warned, "A Synced workflow needs a status for each of: Notable."));
        ownedIssues.Should().ContainSingle().Which.Severity.Should().Be(WorkflowCheckSeverity.Enforced);
        owned.Publish(EventActor.System, At).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Clone_ShouldCopyAManagedWorkflow_AsAnOwnedDraftWithoutItsRetiredStatuses()
    {
        // Arrange
        var workflow = ManagedWorkflow();
        workflow.SyncFromSource("Agile: Bug", [SourceNew, SourceClosed], EventActor.Sync(null), At);

        // Act
        var clone = workflow.Clone("Our Bug Workflow", null, EventActor.System, At);

        // Assert
        clone.Ownership.Should().Be(WorkflowOwnership.Owned);
        clone.ExternalKey.Should().BeNull();
        clone.State.Should().Be(StatusWorkflowState.Draft);
        clone.Statuses.Select(s => (s.Name, s.ExternalKey, s.IsRetired)).Should().Equal(("New", null, false), ("Closed", null, false));
    }

    #endregion Managed
}
