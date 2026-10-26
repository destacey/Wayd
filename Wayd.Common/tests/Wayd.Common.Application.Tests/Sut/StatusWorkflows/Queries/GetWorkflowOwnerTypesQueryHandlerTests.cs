using FluentAssertions;
using Wayd.Common.Application.StatusWorkflows.Queries;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows.Queries;

/// <summary>
/// The owner types a workflow can be built for.
/// </summary>
public sealed class GetWorkflowOwnerTypesQueryHandlerTests : StatusWorkflowHandlerTestBase
{
    [Fact]
    public async Task Handle_ShouldLeaveOutAnOwnerTypeWhoseFeatureIsOff()
    {
        // Arrange
        var sut = new GetWorkflowOwnerTypesQueryHandler(OwnerAvailability);

        // Act
        var owners = await sut.Handle(new GetWorkflowOwnerTypesQuery(), TestContext.Current.CancellationToken);

        // Assert
        owners.Select(o => o.Key).Should().Contain(Widget.Key).And.NotContain(Gadget.Key);
    }

    [Fact]
    public async Task Handle_ShouldIncludeTheOwnerType_OnceItsFeatureIsOn()
    {
        // Arrange
        EnableGadgets();
        var sut = new GetWorkflowOwnerTypesQueryHandler(OwnerAvailability);

        // Act
        var owners = await sut.Handle(new GetWorkflowOwnerTypesQuery(), TestContext.Current.CancellationToken);

        // Assert
        owners.Select(o => o.Key).Should().Contain(Gadget.Key);
    }
}
