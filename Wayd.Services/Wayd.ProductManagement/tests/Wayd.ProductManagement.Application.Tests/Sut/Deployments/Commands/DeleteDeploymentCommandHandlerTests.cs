using FluentAssertions;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.ProductManagement.Application.Deployments.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Tests.Sut.Deployments.Commands;

/// <summary>
/// Deleting a deployment outright, with its status history.
/// </summary>
public sealed class DeleteDeploymentCommandHandlerTests : ProductCommandTestBase
{
    private DeleteDeploymentCommandHandler CreateSut() =>
        new(DbContext, DbContext, CurrentUser.Object, CurrentPrincipal.Object, Logger<DeleteDeploymentCommandHandler>(), DateTimeProvider.Object);

    /// <summary>A deployment whose creation transition has reached the history set, as a save would put it.</summary>
    private Deployment SeedDeploymentWithHistory(EnvironmentCategory category = EnvironmentCategory.Production)
    {
        var deployment = SeedDeployment(category);
        DbContext.AddStatusTransitions(deployment.DrainStatusTransitions());

        return deployment;
    }

    [Fact]
    public async Task Handle_ShouldDeleteTheDeployment()
    {
        // Arrange
        var deployment = SeedDeploymentWithHistory();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DeleteDeploymentCommand(deployment.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        DbContext.Deployments.Should().BeEmpty();
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldDeleteOnlyThatDeploymentsStatusHistory()
    {
        // Arrange
        var deployment = SeedDeploymentWithHistory();
        var other = SeedDeploymentWithHistory(EnvironmentCategory.Staging);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DeleteDeploymentCommand(deployment.Id), TestContext.Current.CancellationToken);

        // Assert
        // The history has no foreign key to the deployment, so nothing cascades to it.
        result.IsSuccess.Should().BeTrue();
        DbContext.StatusTransitions.Should().NotContain(t => t.RecordId == deployment.Id);
        DbContext.StatusTransitions.Should().Contain(t => t.RecordId == other.Id);
    }

    [Fact]
    public async Task Handle_ShouldRaiseDeploymentDeletedEvent()
    {
        // Arrange
        var deployment = SeedDeploymentWithHistory();
        var sut = CreateSut();

        // Act
        await sut.Handle(new DeleteDeploymentCommand(deployment.Id), TestContext.Current.CancellationToken);

        // Assert
        var deleted = deployment.DomainEvents.OfType<DeploymentDeletedEvent>().Should().ContainSingle().Subject;
        deleted.Id.Should().Be(deployment.Id);
        deleted.EnvironmentId.Should().Be(deployment.EnvironmentId);
        deleted.VersionId.Should().Be(deployment.VersionId);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheDeploymentDoesNotExist()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new DeleteDeploymentCommand(Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Deployment not found.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
