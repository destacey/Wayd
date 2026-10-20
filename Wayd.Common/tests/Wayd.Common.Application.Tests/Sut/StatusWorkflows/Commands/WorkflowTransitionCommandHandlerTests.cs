using FluentAssertions;
using Wayd.Common.Application.StatusWorkflows.Commands;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows.Commands;

/// <summary>
/// Setting a workflow's transition mode and replacing the moves it allows.
/// </summary>
public sealed class WorkflowTransitionCommandHandlerTests : StatusWorkflowHandlerTestBase
{
    private SetWorkflowTransitionModeCommandHandler CreateModeSut() =>
        new(DbContext, CurrentUser.Object, DateTimeProvider.Object, Logger<SetWorkflowTransitionModeCommandHandler>());

    private SetWorkflowTransitionsCommandHandler CreateTransitionsSut() =>
        new(DbContext, CurrentUser.Object, DateTimeProvider.Object, Logger<SetWorkflowTransitionsCommandHandler>());

    private static Guid StatusId(StatusWorkflow workflow, string name) => workflow.Statuses.Single(s => s.Name == name).Id;

    [Fact]
    public async Task SetMode_ShouldRestrictTheWorkflow()
    {
        // Arrange
        var workflow = SeedWorkflow();

        // Act
        var result = await CreateModeSut().Handle(
            new SetWorkflowTransitionModeCommand(workflow.Id, TransitionMode.Restricted), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workflow.TransitionMode.Should().Be(TransitionMode.Restricted);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task SetMode_ShouldFail_OnAPublishedWorkflow()
    {
        // Arrange
        var workflow = SeedWorkflow(publish: true);

        // Act
        var result = await CreateModeSut().Handle(
            new SetWorkflowTransitionModeCommand(workflow.Id, TransitionMode.Restricted), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only draft workflows can be restructured.");
        workflow.TransitionMode.Should().Be(TransitionMode.AnyToAny);
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task SetMode_ShouldFail_WhenTheWorkflowDoesNotExist()
    {
        // Arrange
        SeedWorkflow();

        // Act
        var result = await CreateModeSut().Handle(
            new SetWorkflowTransitionModeCommand(Guid.NewGuid(), TransitionMode.Restricted), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Status workflow not found.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task SetTransitions_ShouldReplaceTheMovesTheWorkflowAllows()
    {
        // Arrange
        var workflow = SeedWorkflow();
        var (proposed, notable, terminal) = (StatusId(workflow, "Proposed"), StatusId(workflow, "Notable"), StatusId(workflow, "Terminal"));

        // Act
        var result = await CreateTransitionsSut().Handle(
            new SetWorkflowTransitionsCommand(workflow.Id, [new(proposed, notable), new(notable, terminal)]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workflow.Transitions.Select(t => (t.FromStatusId, t.ToStatusId)).Should().BeEquivalentTo([(proposed, notable), (notable, terminal)]);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task SetTransitions_ShouldFail_ForAStatusOutsideTheWorkflow()
    {
        // Arrange
        var workflow = SeedWorkflow();
        var other = SeedWorkflow("Other Widget Workflow");

        // Act
        var result = await CreateTransitionsSut().Handle(
            new SetWorkflowTransitionsCommand(workflow.Id, [new(StatusId(workflow, "Proposed"), StatusId(other, "Notable"))]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Every transition must be between statuses in this workflow.");
        workflow.Transitions.Should().BeEmpty();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task SetTransitions_ShouldFail_OnAPublishedWorkflow()
    {
        // Arrange
        var workflow = SeedWorkflow(publish: true);

        // Act
        var result = await CreateTransitionsSut().Handle(
            new SetWorkflowTransitionsCommand(workflow.Id, [new(StatusId(workflow, "Proposed"), StatusId(workflow, "Notable"))]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only draft workflows can be restructured.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
