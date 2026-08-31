using FluentAssertions;
using Wayd.Common.Application.StatusWorkflows.Commands;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows.Commands;

/// <summary>
/// Adding, renaming, reclassifying, removing and reordering a workflow's statuses.
/// </summary>
public sealed class WorkflowStatusCommandHandlerTests : StatusWorkflowHandlerTestBase
{
    [Fact]
    public async Task Add_ShouldAppendAStatus()
    {
        // Arrange
        var workflow = SeedWorkflow();
        var sut = new AddWorkflowStatusCommandHandler(DbContext, Logger<AddWorkflowStatusCommandHandler>());

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
        var sut = new AddWorkflowStatusCommandHandler(DbContext, Logger<AddWorkflowStatusCommandHandler>());

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
        var sut = new AddWorkflowStatusCommandHandler(DbContext, Logger<AddWorkflowStatusCommandHandler>());

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
        var sut = new RenameWorkflowStatusCommandHandler(DbContext, Logger<RenameWorkflowStatusCommandHandler>());

        // Act
        var result = await sut.Handle(
            new RenameWorkflowStatusCommand(workflow.Id, status.Id, "Considered", "Reworded."),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workflow.Statuses.Should().Contain(s => s.Name == "Considered");
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
        var sut = new RemoveWorkflowStatusCommandHandler(DbContext, Logger<RemoveWorkflowStatusCommandHandler>());

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
        var sut = new ReorderWorkflowStatusesCommandHandler(DbContext, Logger<ReorderWorkflowStatusesCommandHandler>());

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
        var sut = new ReorderWorkflowStatusesCommandHandler(DbContext, Logger<ReorderWorkflowStatusesCommandHandler>());

        // Act
        var result = await sut.Handle(
            new ReorderWorkflowStatusesCommand(workflow.Id, reversed), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workflow.Statuses.Select(s => s.Id).Should().ContainInOrder(reversed);
    }
}
