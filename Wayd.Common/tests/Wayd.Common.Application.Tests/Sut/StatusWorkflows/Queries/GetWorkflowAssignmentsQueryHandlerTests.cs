using FluentAssertions;
using Wayd.Tests.Shared.Data;
using Wayd.Common.Application.StatusWorkflows.Queries;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows.Queries;

/// <summary>
/// Which workflow governs each scope, as workflow administration lists it.
/// </summary>
public sealed class GetWorkflowAssignmentsQueryHandlerTests : StatusWorkflowHandlerTestBase
{
    private WorkflowAssignment SeedAssignment(WorkflowOwnerDescriptor owner)
    {
        var workflow = SeedWorkflow($"{owner.DisplayName} Workflow", publish: true, owner: owner);
        var assignment = TestWorkflowAssignments.ForWorkflow(owner.Key, null, workflow, EventActor.System, Now).Value;
        assignment.ClearDomainEvents();
        DbContext.AddWorkflowAssignment(assignment);

        return assignment;
    }

    [Fact]
    public async Task Handle_ShouldLeaveOutAssignmentsWhoseFeatureIsOff()
    {
        // Arrange
        var widgets = SeedAssignment(Widget);
        var gadgets = SeedAssignment(Gadget);
        var sut = new GetWorkflowAssignmentsQueryHandler(DbContext, OwnerAvailability);

        // Act
        var assignments = await sut.Handle(new GetWorkflowAssignmentsQuery(null), TestContext.Current.CancellationToken);

        // Assert
        assignments.Select(a => a.Id).Should().Contain(widgets.Id).And.NotContain(gadgets.Id);
    }

    [Fact]
    public async Task Handle_ShouldListTheAssignment_OnceItsFeatureIsOn()
    {
        // Arrange
        EnableGadgets();
        var gadgets = SeedAssignment(Gadget);
        var sut = new GetWorkflowAssignmentsQueryHandler(DbContext, OwnerAvailability);

        // Act
        var assignments = await sut.Handle(new GetWorkflowAssignmentsQuery(Gadget.Key), TestContext.Current.CancellationToken);

        // Assert
        assignments.Select(a => a.Id).Should().Equal(gadgets.Id);
    }
}
