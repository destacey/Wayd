using FluentAssertions;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows;

/// <summary>
/// Which owner types workflow administration may show, by the feature flag each is released behind.
/// </summary>
public sealed class WorkflowOwnerAvailabilityTests : StatusWorkflowHandlerTestBase
{
    [Fact]
    public async Task Unavailable_ShouldHoldAnOwnerTypeWhoseFeatureIsOff()
    {
        // Arrange — flags read as off unless a test turns one on

        // Act
        var unavailable = await OwnerAvailability.Unavailable(TestContext.Current.CancellationToken);

        // Assert
        unavailable.Should().Contain(Gadget.Key);
        unavailable.Should().NotContain(Widget.Key, "an owner type released behind no flag is always available");
    }

    [Fact]
    public async Task IsAvailable_ShouldBeTrue_OnceTheFeatureIsOn()
    {
        // Arrange
        EnableGadgets();

        // Act
        var available = await OwnerAvailability.IsAvailable(Gadget.Key, TestContext.Current.CancellationToken);

        // Assert
        available.Should().BeTrue();
    }

    [Fact]
    public async Task IsAvailable_ShouldBeTrue_ForAnOwnerTypeNoModuleRegisters()
    {
        // A workflow can outlive its module; administration shows it under its raw key rather than hiding it.
        // Act
        var available = await OwnerAvailability.IsAvailable("test.nobody", TestContext.Current.CancellationToken);

        // Assert
        available.Should().BeTrue();
    }
}
