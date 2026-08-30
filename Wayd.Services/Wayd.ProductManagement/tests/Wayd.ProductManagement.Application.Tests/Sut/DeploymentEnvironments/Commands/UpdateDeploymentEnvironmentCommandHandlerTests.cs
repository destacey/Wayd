using FluentAssertions;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.ProductManagement.Application.DeploymentEnvironments.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.DeploymentEnvironments.Commands;

/// <summary>
/// Editing an environment. Reclassifying is the consequential half: delivery measures scoped to
/// production count on the category, not the name.
/// </summary>
public sealed class UpdateDeploymentEnvironmentCommandHandlerTests : ProductCommandTestBase
{
    private UpdateDeploymentEnvironmentCommandHandler UpdateSut() =>
        new(DbContext, CurrentUser.Object, Logger<UpdateDeploymentEnvironmentCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldRenameAndReorder()
    {
        // Arrange
        var environment = SeedEnvironment("prod-eu", EnvironmentCategory.Production, 3);
        var sut = UpdateSut();

        // Act
        var result = await sut.Handle(
            new UpdateDeploymentEnvironmentCommand(environment.Id, "Production EU", EnvironmentCategory.Production, 5),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        environment.Name.Should().Be("Production EU");
        environment.RingOrder.Should().Be(5);
    }

    [Fact]
    public async Task Handle_ShouldRaiseAnEventWhenReclassified()
    {
        // Arrange
        var environment = SeedEnvironment("Staging", EnvironmentCategory.Staging, 2);
        var sut = UpdateSut();

        // Act
        await sut.Handle(
            new UpdateDeploymentEnvironmentCommand(environment.Id, "Staging", EnvironmentCategory.Production, 2),
            TestContext.Current.CancellationToken);

        // Assert
        // Moving an environment into Production changes what every past deployment to it counts toward,
        // so the change is announced rather than buried in a rename.
        environment.Category.Should().Be(EnvironmentCategory.Production);
        environment.DomainEvents.OfType<EnvironmentReclassifiedEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ShouldRaiseNoReclassifyEvent_WhenTheCategoryIsUnchanged()
    {
        // Arrange
        var environment = SeedEnvironment("Staging", EnvironmentCategory.Staging, 2);
        var sut = UpdateSut();

        // Act
        await sut.Handle(
            new UpdateDeploymentEnvironmentCommand(environment.Id, "Staging 2", EnvironmentCategory.Staging, 2),
            TestContext.Current.CancellationToken);

        // Assert
        environment.DomainEvents.OfType<EnvironmentReclassifiedEvent>().Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldFail_OnAnotherEnvironmentsName()
    {
        // Arrange
        SeedEnvironment("Production");
        var environment = SeedEnvironment("Staging", EnvironmentCategory.Staging, 2);
        var sut = UpdateSut();

        // Act
        var result = await sut.Handle(
            new UpdateDeploymentEnvironmentCommand(environment.Id, "Production", EnvironmentCategory.Staging, 2),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("An environment named 'Production' already exists.");
    }
}
