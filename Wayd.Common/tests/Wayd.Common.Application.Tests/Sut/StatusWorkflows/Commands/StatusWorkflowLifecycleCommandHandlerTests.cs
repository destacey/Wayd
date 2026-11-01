using FluentAssertions;
using Wayd.Tests.Shared.Data;
using Wayd.Common.Application.StatusWorkflows.Commands;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows.Commands;

/// <summary>
/// Creating, publishing, archiving and cloning a workflow.
/// </summary>
public sealed class StatusWorkflowLifecycleCommandHandlerTests : StatusWorkflowHandlerTestBase
{
    [Fact]
    public async Task Create_ShouldAddADraftWorkflow()
    {
        // Arrange
        var sut = new CreateStatusWorkflowCommandHandler(DbContext, CurrentUser.Object, DateTimeProvider.Object, Logger<CreateStatusWorkflowCommandHandler>(), OwnerAvailability);

        // Act
        var result = await sut.Handle(
            new CreateStatusWorkflowCommand("Widget Workflow", null, Widget.Key),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Create_ShouldFail_ForAnUnregisteredOwnerType()
    {
        // A workflow for an owner type nothing registered could never be resolved at runtime.
        // Arrange
        var sut = new CreateStatusWorkflowCommandHandler(DbContext, CurrentUser.Object, DateTimeProvider.Object, Logger<CreateStatusWorkflowCommandHandler>(), OwnerAvailability);

        // Act
        var result = await sut.Handle(
            new CreateStatusWorkflowCommand("Orphan", null, "test.nobody"),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Publish_ShouldMoveADraftToPublished()
    {
        // Arrange
        var workflow = SeedWorkflow();
        var sut = CreatePublishSut();

        // Act
        var result = await sut.Handle(
            new PublishStatusWorkflowCommand(workflow.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workflow.State.Should().Be(StatusWorkflowState.Published);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Publish_ShouldFail_WhenARequiredAliasIsMissing()
    {
        // Arrange
        var workflow = StatusWorkflow.Create("Partial", null, Widget.Key, EventActor.System, Now).Value;
        workflow.AddStatus("Proposed", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, Now);
        DbContext.AddStatusWorkflow(workflow);

        var sut = CreatePublishSut();

        // Act
        var result = await sut.Handle(
            new PublishStatusWorkflowCommand(workflow.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Publish_ShouldFail_WhenTheWorkflowDoesNotExist()
    {
        // Arrange
        var sut = CreatePublishSut();

        // Act
        var result = await sut.Handle(
            new PublishStatusWorkflowCommand(Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Status workflow not found.");
    }

    [Fact]
    public async Task Archive_ShouldFail_WhileTheWorkflowIsStillAssigned()
    {
        // Something has to govern those records; archiving out from under them would strand them.
        // Arrange
        var workflow = SeedWorkflow(publish: true);
        var assignment = TestWorkflowAssignments.ForWorkflow(Widget.Key, null, workflow, EventActor.System, Now).Value;
        DbContext.AddWorkflowAssignment(assignment);

        var sut = new ArchiveStatusWorkflowCommandHandler(
            DbContext, CurrentUser.Object, DateTimeProvider.Object, Logger<ArchiveStatusWorkflowCommandHandler>());

        // Act
        var result = await sut.Handle(
            new ArchiveStatusWorkflowCommand(workflow.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Archive_ShouldFail_WhileAnUnassignedSchemeStillNamesTheWorkflow()
    {
        // Assigning that scheme later would move records onto an archived workflow.
        // Arrange
        var ticket = new WorkflowOwnerDescriptor(
            "test.lifecycle.ticket", "Ticket", new Dictionary<int, WorkflowAlias>(), [], hasRecordTypes: true);
        WorkflowOwners.Register(ticket);
        var standard = SeedWorkflow("Standard", publish: true, owner: ticket);
        var bugFlow = SeedWorkflow("Bug Flow", publish: true, owner: ticket);
        var bug = RecordType.Create(ticket.Key, "Bug", EventActor.System, Now).Value;
        var scheme = StatusWorkflowScheme.Create(ticket.Key, "Engineering", null, standard, EventActor.System, Now).Value;
        scheme.SetEntry(bug, bugFlow, StatusRemap.AutoMap(standard, bugFlow).Value, EventActor.System, Now);
        DbContext.AddStatusWorkflowScheme(scheme);

        var sut = new ArchiveStatusWorkflowCommandHandler(
            DbContext, CurrentUser.Object, DateTimeProvider.Object, Logger<ArchiveStatusWorkflowCommandHandler>());

        // Act
        var result = await sut.Handle(
            new ArchiveStatusWorkflowCommand(bugFlow.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This workflow is part of the workflow scheme(s) 'Engineering'. Remove it from them first.");
        bugFlow.State.Should().Be(StatusWorkflowState.Published);
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Archive_ShouldSucceed_WhenNothingUsesIt()
    {
        // Arrange
        var workflow = SeedWorkflow(publish: true);
        var sut = new ArchiveStatusWorkflowCommandHandler(
            DbContext, CurrentUser.Object, DateTimeProvider.Object, Logger<ArchiveStatusWorkflowCommandHandler>());

        // Act
        var result = await sut.Handle(
            new ArchiveStatusWorkflowCommand(workflow.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workflow.State.Should().Be(StatusWorkflowState.Archived);
    }

    [Fact]
    public async Task Clone_ShouldCopyEveryStatusIntoANewDraft()
    {
        // Cloning is how a published or seeded workflow is changed, so the copy has to be complete.
        // Arrange
        var workflow = SeedWorkflow(publish: true);
        var sut = new CloneStatusWorkflowCommandHandler(DbContext, CurrentUser.Object, DateTimeProvider.Object, Logger<CloneStatusWorkflowCommandHandler>(), OwnerAvailability);

        // Act
        var result = await sut.Handle(
            new CloneStatusWorkflowCommand(workflow.Id, "Widget Workflow v2", null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var clone = DbContext.StatusWorkflows.Single(w => w.Id == result.Value);
        clone.State.Should().Be(StatusWorkflowState.Draft);
        clone.Statuses.Select(s => s.Name).Should().BeEquivalentTo(workflow.Statuses.Select(s => s.Name));
        clone.Statuses.Select(s => s.Id).Should().NotIntersectWith(workflow.Statuses.Select(s => s.Id));
    }

    [Fact]
    public async Task Create_ShouldFail_ForAnOwnerTypeWhoseFeatureIsOff()
    {
        // An unreleased module's owner type reads as one nothing registered, so Settings never offers it.
        // Arrange
        var sut = new CreateStatusWorkflowCommandHandler(DbContext, CurrentUser.Object, DateTimeProvider.Object, Logger<CreateStatusWorkflowCommandHandler>(), OwnerAvailability);

        // Act
        var result = await sut.Handle(
            new CreateStatusWorkflowCommand("Gadget Workflow", null, Gadget.Key),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be($"'{Gadget.Key}' is not a registered workflow owner type.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Create_ShouldAddAWorkflow_OnceTheOwnerTypesFeatureIsOn()
    {
        // Arrange
        EnableGadgets();
        var sut = new CreateStatusWorkflowCommandHandler(DbContext, CurrentUser.Object, DateTimeProvider.Object, Logger<CreateStatusWorkflowCommandHandler>(), OwnerAvailability);

        // Act
        var result = await sut.Handle(
            new CreateStatusWorkflowCommand("Gadget Workflow", null, Gadget.Key),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Clone_ShouldFail_ForAWorkflowWhoseFeatureIsOff()
    {
        // Arrange
        var workflow = SeedWorkflow("Gadget Workflow", publish: true, owner: Gadget);
        var sut = new CloneStatusWorkflowCommandHandler(DbContext, CurrentUser.Object, DateTimeProvider.Object, Logger<CloneStatusWorkflowCommandHandler>(), OwnerAvailability);

        // Act
        var result = await sut.Handle(
            new CloneStatusWorkflowCommand(workflow.Id, "Gadget Workflow v2", null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Status workflow not found.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    private PublishStatusWorkflowCommandHandler CreatePublishSut() =>
        new(DbContext, CurrentUser.Object, DateTimeProvider.Object, Logger<PublishStatusWorkflowCommandHandler>());
}
