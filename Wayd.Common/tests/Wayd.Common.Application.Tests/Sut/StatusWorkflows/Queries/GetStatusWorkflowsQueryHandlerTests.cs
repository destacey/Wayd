using FluentAssertions;
using Wayd.Common.Application.StatusWorkflows.Queries;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows.Queries;

/// <summary>
/// The workflows workflow administration lists.
/// </summary>
public sealed class GetStatusWorkflowsQueryHandlerTests : StatusWorkflowHandlerTestBase
{
    [Fact]
    public async Task Handle_ShouldLeaveOutWorkflowsWhoseFeatureIsOff()
    {
        // Arrange
        var widgets = SeedWorkflow("Widget Workflow");
        var gadgets = SeedWorkflow("Gadget Workflow", owner: Gadget);
        var sut = new GetStatusWorkflowsQueryHandler(DbContext, OwnerAvailability);

        // Act
        var workflows = await sut.Handle(new GetStatusWorkflowsQuery(null, null), TestContext.Current.CancellationToken);

        // Assert
        workflows.Select(w => w.Id).Should().Contain(widgets.Id).And.NotContain(gadgets.Id);
    }

    [Fact]
    public async Task Handle_ShouldLeaveOutWorkflowsWhoseFeatureIsOff_WhenAskedForThatOwnerType()
    {
        // Arrange
        SeedWorkflow("Gadget Workflow", owner: Gadget);
        var sut = new GetStatusWorkflowsQueryHandler(DbContext, OwnerAvailability);

        // Act
        var workflows = await sut.Handle(new GetStatusWorkflowsQuery(Gadget.Key, null), TestContext.Current.CancellationToken);

        // Assert
        workflows.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldListTheWorkflows_OnceTheirFeatureIsOn()
    {
        // Arrange
        EnableGadgets();
        var gadgets = SeedWorkflow("Gadget Workflow", owner: Gadget);
        var sut = new GetStatusWorkflowsQueryHandler(DbContext, OwnerAvailability);

        // Act
        var workflows = await sut.Handle(new GetStatusWorkflowsQuery(null, null), TestContext.Current.CancellationToken);

        // Assert
        workflows.Select(w => w.Id).Should().Contain(gadgets.Id);
    }
}
