using FluentAssertions;
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
        var sut = new CreateStatusWorkflowCommandHandler(DbContext, Logger<CreateStatusWorkflowCommandHandler>());

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
        var sut = new CreateStatusWorkflowCommandHandler(DbContext, Logger<CreateStatusWorkflowCommandHandler>());

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
        var workflow = StatusWorkflow.Create("Partial", null, Widget.Key).Value;
        workflow.AddStatus("Proposed", null, StatusCategory.Proposed);
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
        var assignment = WorkflowAssignment.Create(Widget.Key, null, workflow, EventActor.System, Now).Value;
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
        var sut = new CloneStatusWorkflowCommandHandler(DbContext, Logger<CloneStatusWorkflowCommandHandler>());

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

    private PublishStatusWorkflowCommandHandler CreatePublishSut() =>
        new(DbContext, CurrentUser.Object, DateTimeProvider.Object, Logger<PublishStatusWorkflowCommandHandler>());
}
