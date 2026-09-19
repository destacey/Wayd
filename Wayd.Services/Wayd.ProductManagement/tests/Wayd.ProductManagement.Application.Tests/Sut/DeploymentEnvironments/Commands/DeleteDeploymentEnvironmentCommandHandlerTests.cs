using FluentAssertions;
using Moq;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.ProductManagement.Application.DeploymentEnvironments.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.DeploymentEnvironments.Commands;

/// <summary>
/// Deleting an environment, which takes every deployment into it along.
/// </summary>
public sealed class DeleteDeploymentEnvironmentCommandHandlerTests : ProductCommandTestBase
{
    private const string DeleteDeliveryPermission = "Permissions.Delivery.Delete";

    public DeleteDeploymentEnvironmentCommandHandlerTests()
    {
        GrantDeleteDelivery(true);
    }

    private void GrantDeleteDelivery(bool granted) =>
        CurrentPrincipal
            .Setup(p => p.HasPermission(DeleteDeliveryPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(granted);

    private DeleteDeploymentEnvironmentCommandHandler CreateSut() =>
        new(DbContext, DbContext, CurrentUser.Object, CurrentPrincipal.Object, Logger<DeleteDeploymentEnvironmentCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldDeleteAnEnvironmentWithNoDeployments()
    {
        // Arrange
        var environment = SeedEnvironment();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new DeleteDeploymentEnvironmentCommand(environment.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        DbContext.DeploymentEnvironments.Should().BeEmpty();
        environment.DomainEvents.OfType<EnvironmentDeletedEvent>().Should().ContainSingle()
            .Which.Category.Should().Be(EnvironmentCategory.Production);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldDeleteEveryDeploymentIntoItAndTheirHistory()
    {
        // Arrange
        var environment = SeedEnvironment();
        var first = SeedDeployment(environment: environment);
        var second = SeedDeployment(environment: environment);
        var elsewhere = SeedDeployment(EnvironmentCategory.Staging);
        foreach (var deployment in new[] { first, second, elsewhere })
        {
            DbContext.AddStatusTransitions(deployment.DrainStatusTransitions());
        }

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new DeleteDeploymentEnvironmentCommand(environment.Id), TestContext.Current.CancellationToken);

        // Assert
        // Deployments restrict on the environment, so they must go in the same save.
        result.IsSuccess.Should().BeTrue();
        DbContext.Deployments.Should().ContainSingle().Which.Id.Should().Be(elsewhere.Id);
        DbContext.StatusTransitions.Should().OnlyContain(t => t.RecordId == elsewhere.Id);
        DbContext.DeploymentEnvironments.Should().NotContain(e => e.Id == environment.Id);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRaiseADeletedEventForEachDeployment()
    {
        // Arrange
        var environment = SeedEnvironment();
        var first = SeedDeployment(environment: environment);
        var second = SeedDeployment(environment: environment);
        var sut = CreateSut();

        // Act
        await sut.Handle(new DeleteDeploymentEnvironmentCommand(environment.Id), TestContext.Current.CancellationToken);

        // Assert
        // Each deployment's own activity entry, rather than one list on the environment's.
        first.DomainEvents.OfType<DeploymentDeletedEvent>().Should().ContainSingle();
        second.DomainEvents.OfType<DeploymentDeletedEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenItHasDeploymentsAndTheViewerCannotDeleteDeployments()
    {
        // Arrange
        GrantDeleteDelivery(false);
        var environment = SeedEnvironment();
        SeedDeployment(environment: environment);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new DeleteDeploymentEnvironmentCommand(environment.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(
            "Deployments into this environment would be deleted with it (1), which also needs permission to delete deployments.");
        DbContext.DeploymentEnvironments.Should().ContainSingle(e => e.Id == environment.Id);
        DbContext.Deployments.Should().ContainSingle();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldNotNeedDeploymentDelete_WhenItHasNoDeployments()
    {
        // Arrange
        GrantDeleteDelivery(false);
        var environment = SeedEnvironment();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new DeleteDeploymentEnvironmentCommand(environment.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        DbContext.DeploymentEnvironments.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheEnvironmentDoesNotExist()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new DeleteDeploymentEnvironmentCommand(Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Deployment environment not found.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
