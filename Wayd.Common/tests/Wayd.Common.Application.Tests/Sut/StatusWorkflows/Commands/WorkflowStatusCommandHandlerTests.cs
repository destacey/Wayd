using FluentAssertions;
using Wayd.Tests.Shared.Extensions;
using Wayd.Common.Application.StatusWorkflows.Commands;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows.Commands;

/// <summary>
/// Adding, renaming, describing, reclassifying, removing and reordering a workflow's statuses.
/// </summary>
public sealed class WorkflowStatusCommandHandlerTests : StatusWorkflowHandlerTestBase
{
    // An owner type whose workflows a sync keeps.
    private static readonly WorkflowOwnerDescriptor Synced = new(
        "test.status-synced",
        "Synced",
        new Dictionary<int, WorkflowAlias>(),
        [],
        hasManagedWorkflows: true);

    public WorkflowStatusCommandHandlerTests() => WorkflowOwners.Register(Synced);

    [Fact]
    public async Task Add_ShouldAppendAStatus()
    {
        // Arrange
        var workflow = SeedWorkflow();
        var sut = new AddWorkflowStatusCommandHandler(DbContext, CurrentUser.Object, DateTimeProvider.Object, Logger<AddWorkflowStatusCommandHandler>());

        // Act
        var result = await sut.Handle(
            new AddWorkflowStatusCommand(workflow.Id, "Blocked", null, StatusCategory.Active, 0),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workflow.Statuses.Should().Contain(s => s.Name == "Blocked");
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Add_ShouldFail_OnADuplicateName()
    {
        // Arrange
        var workflow = SeedWorkflow();
        var sut = new AddWorkflowStatusCommandHandler(DbContext, CurrentUser.Object, DateTimeProvider.Object, Logger<AddWorkflowStatusCommandHandler>());

        // Act
        var result = await sut.Handle(
            new AddWorkflowStatusCommand(workflow.Id, "Proposed", null, StatusCategory.Proposed, 0),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Add_ShouldFail_OnAPublishedWorkflow()
    {
        // Arrange
        var workflow = SeedWorkflow(publish: true);
        var sut = new AddWorkflowStatusCommandHandler(DbContext, CurrentUser.Object, DateTimeProvider.Object, Logger<AddWorkflowStatusCommandHandler>());

        // Act
        var result = await sut.Handle(
            new AddWorkflowStatusCommand(workflow.Id, "Blocked", null, StatusCategory.Active, 0),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Rename_ShouldChangeTheName()
    {
        // Arrange
        var workflow = SeedWorkflow();
        var status = workflow.Statuses.First();
        var sut = new RenameWorkflowStatusCommandHandler(DbContext, CurrentUser.Object, DateTimeProvider.Object, Logger<RenameWorkflowStatusCommandHandler>());

        // Act
        var result = await sut.Handle(
            new RenameWorkflowStatusCommand(workflow.Id, status.Id, "Considered", "Reworded."),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workflow.Statuses.Should().Contain(s => s.Name == "Considered");
    }

    [Fact]
    public async Task Rename_ShouldFail_OnAManagedWorkflow()
    {
        // The source owns a managed status's name.
        // Arrange
        var workflow = SeedManagedWorkflow();
        var status = workflow.Statuses.Single();
        var sut = new RenameWorkflowStatusCommandHandler(DbContext, CurrentUser.Object, DateTimeProvider.Object, Logger<RenameWorkflowStatusCommandHandler>());

        // Act
        var result = await sut.Handle(
            new RenameWorkflowStatusCommand(workflow.Id, status.Id, "Fresh", "Not started yet."),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        (status.Name, status.Description).Should().Be(("New", null));
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Describe_ShouldChangeOnlyTheDescription_OfAManagedWorkflowsStatus()
    {
        // Arrange
        var workflow = SeedManagedWorkflow();
        var status = workflow.Statuses.Single();
        var sut = new DescribeWorkflowStatusCommandHandler(DbContext, CurrentUser.Object, DateTimeProvider.Object, Logger<DescribeWorkflowStatusCommandHandler>());

        // Act
        var result = await sut.Handle(
            new DescribeWorkflowStatusCommand(workflow.Id, status.Id, "Not started yet."),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        (status.Name, status.Description).Should().Be(("New", "Not started yet."));
        var changed = workflow.DomainEvents.OfType<WorkflowStatusDescriptionChangedEvent>().Should().ContainSingle().Subject;
        (changed.StatusId, changed.FromDescription, changed.ToDescription).Should().Be((status.Id, null, "Not started yet."));
        changed.Actor.Kind.Should().Be(EventActorKind.User, "the Activity log attributes the change to the administrator");
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Describe_ShouldClearTheDescription_WhenGivenNone()
    {
        // Arrange
        var workflow = SeedManagedWorkflow();
        var status = workflow.Statuses.Single();
        workflow.DescribeStatus(status.Id, "Not started yet.", EventActor.Sync(null), Now);
        workflow.ClearDomainEvents();
        var sut = new DescribeWorkflowStatusCommandHandler(DbContext, CurrentUser.Object, DateTimeProvider.Object, Logger<DescribeWorkflowStatusCommandHandler>());

        // Act
        var result = await sut.Handle(
            new DescribeWorkflowStatusCommand(workflow.Id, status.Id, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        status.Description.Should().BeNull();
        var changed = workflow.DomainEvents.OfType<WorkflowStatusDescriptionChangedEvent>().Should().ContainSingle().Subject;
        (changed.FromDescription, changed.ToDescription).Should().Be(("Not started yet.", (string?)null));
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Describe_ShouldFail_ForAnUnknownStatus()
    {
        // Arrange
        var workflow = SeedManagedWorkflow();
        var sut = new DescribeWorkflowStatusCommandHandler(DbContext, CurrentUser.Object, DateTimeProvider.Object, Logger<DescribeWorkflowStatusCommandHandler>());

        // Act
        var result = await sut.Handle(
            new DescribeWorkflowStatusCommand(workflow.Id, Guid.NewGuid(), "Not started yet."),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Describe_ShouldFail_ForAnUnknownWorkflow()
    {
        // Arrange
        var sut = new DescribeWorkflowStatusCommandHandler(DbContext, CurrentUser.Object, DateTimeProvider.Object, Logger<DescribeWorkflowStatusCommandHandler>());

        // Act
        var result = await sut.Handle(
            new DescribeWorkflowStatusCommand(Guid.NewGuid(), Guid.NewGuid(), "Not started yet."),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Status workflow not found.");
    }

    [Fact]
    public async Task Reclassify_ShouldSetTheCategoryAndAlias()
    {
        // The recovery path when a workflow cannot publish for a missing required alias.
        // Arrange
        var workflow = SeedWorkflow();
        var status = workflow.Statuses.Single(s => s.Name == "Proposed");
        var sut = new ReclassifyWorkflowStatusCommandHandler(
            DbContext, CurrentUser.Object, DateTimeProvider.Object, Logger<ReclassifyWorkflowStatusCommandHandler>());

        // Act
        var result = await sut.Handle(
            new ReclassifyWorkflowStatusCommand(workflow.Id, status.Id, StatusCategory.Active, 0),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workflow.Statuses.Single(s => s.Id == status.Id).Category.Should().Be(StatusCategory.Active);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Remove_ShouldDropTheStatus()
    {
        // Arrange
        var workflow = SeedWorkflow();
        var status = workflow.Statuses.Single(s => s.Name == "Proposed");
        var sut = new RemoveWorkflowStatusCommandHandler(DbContext, CurrentUser.Object, DateTimeProvider.Object, Logger<RemoveWorkflowStatusCommandHandler>());

        // Act
        var result = await sut.Handle(
            new RemoveWorkflowStatusCommand(workflow.Id, status.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workflow.Statuses.Should().NotContain(s => s.Name == "Proposed");
    }

    [Fact]
    public async Task Reorder_ShouldFail_OnAPartialList()
    {
        // The aggregate demands every id, so a caller moving one status still sends them all.
        // Arrange
        var workflow = SeedWorkflow();
        var sut = new ReorderWorkflowStatusesCommandHandler(DbContext, CurrentUser.Object, DateTimeProvider.Object, Logger<ReorderWorkflowStatusesCommandHandler>());

        // Act
        var result = await sut.Handle(
            new ReorderWorkflowStatusesCommand(workflow.Id, [workflow.Statuses.First().Id]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Reorder_ShouldReverseTheOrder_WhenGivenEveryId()
    {
        // Arrange
        var workflow = SeedWorkflow();
        var reversed = workflow.Statuses.Select(s => s.Id).Reverse().ToList();
        var sut = new ReorderWorkflowStatusesCommandHandler(DbContext, CurrentUser.Object, DateTimeProvider.Object, Logger<ReorderWorkflowStatusesCommandHandler>());

        // Act
        var result = await sut.Handle(
            new ReorderWorkflowStatusesCommand(workflow.Id, reversed), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workflow.Statuses.Select(s => s.Id).Should().ContainInOrder(reversed);
    }

    /// <summary>A managed workflow, as a sync creates it, with one status.</summary>
    private StatusWorkflow SeedManagedWorkflow()
    {
        var workflow = StatusWorkflow.CreateManaged(
            "Agile: Bug", Synced.Key, "conn-1:agile:bug", [new("new", "New", StatusCategory.Proposed)], EventActor.Sync(null), Now).Value;
        // As saved: a workflow raises its events once it has a key.
        workflow.SetPrivate(w => w.Key, 9);
        workflow.ClearDomainEvents();
        DbContext.AddStatusWorkflow(workflow);
        return workflow;
    }
}
