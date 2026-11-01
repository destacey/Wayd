using FluentAssertions;
using Wayd.Tests.Shared.Data;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows;

/// <summary>
/// Resolving a status from the workflow governing a scope, carrying the moves that workflow allows.
/// </summary>
public sealed class StatusResolverTests : StatusWorkflowHandlerTestBase
{
    private StatusResolver CreateSut() => new(DbContext);

    private StatusWorkflow SeedAssignedWorkflow(bool restricted)
    {
        var workflow = SeedWorkflow();
        var (proposed, notable, terminal) = (StatusId(workflow, "Proposed"), StatusId(workflow, "Notable"), StatusId(workflow, "Terminal"));

        if (restricted)
        {
            workflow.SetTransitionMode(TransitionMode.Restricted, EventActor.System, Now);
            workflow.SetTransitions([new(proposed, notable), new(notable, terminal)], EventActor.System, Now);
        }

        workflow.Publish(EventActor.System, Now).IsSuccess.Should().BeTrue();

        var assignment = TestWorkflowAssignments.ForWorkflow(Widget.Key, null, workflow, EventActor.System, Now).Value;
        DbContext.AddWorkflowAssignment(assignment);

        return workflow;
    }

    private static Guid StatusId(StatusWorkflow workflow, string name) => workflow.Statuses.Single(s => s.Name == name).Id;

    [Fact]
    public async Task ForAlias_ShouldCarryTheStatusesAMoveIsAllowedFrom_WhenRestricted()
    {
        // Arrange
        var workflow = SeedAssignedWorkflow(restricted: true);

        // Act
        var result = await CreateSut().ForAlias(Widget.Key, null, TerminalAlias, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.StatusId.Should().Be(StatusId(workflow, "Terminal"));
        result.Value.AllowedFrom.Should().BeEquivalentTo([StatusId(workflow, "Notable")]);
    }

    [Fact]
    public async Task ForAlias_ShouldAllowAnyMove_WhenTheWorkflowAllowsAny()
    {
        // Arrange
        SeedAssignedWorkflow(restricted: false);

        // Act
        var result = await CreateSut().ForAlias(Widget.Key, null, TerminalAlias, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AllowedFrom.Should().BeNull();
    }

    [Fact]
    public async Task Initial_ShouldCarryTheStatusesAMoveIsAllowedFrom_WhenRestricted()
    {
        // Arrange
        var workflow = SeedAssignedWorkflow(restricted: true);

        // Act
        var result = await CreateSut().Initial(Widget.Key, null, TestContext.Current.CancellationToken);

        // Assert
        // Nothing moves into the initial status here, which is harmless: a record entering it is exempt.
        result.IsSuccess.Should().BeTrue();
        result.Value.StatusId.Should().Be(StatusId(workflow, "Proposed"));
        result.Value.AllowedFrom.Should().BeEmpty();
    }
}
