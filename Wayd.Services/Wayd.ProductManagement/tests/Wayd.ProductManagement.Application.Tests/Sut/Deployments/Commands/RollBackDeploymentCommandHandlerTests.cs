using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Deployments.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.Tests.Sut.Deployments.Commands;

/// <summary>
/// Reverting a deployment that reached its environment.
/// </summary>
public sealed class RollBackDeploymentCommandHandlerTests : ProductCommandTestBase
{
    private readonly Mock<IStatusResolver> _statusResolver = new();

    public RollBackDeploymentCommandHandlerTests()
    {
        _statusResolver
            .Setup(r => r.ForAlias(
                ProductWorkflowOwners.Deployment.Key, null, (int)ProductStatusAlias.RolledBack, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result.Success(
                Status("Rolled Back", StatusCategory.Removed, ProductStatusAlias.RolledBack)));

        _statusResolver
            .Setup(r => r.ForAlias(
                ProductWorkflowOwners.Deployment.Key, null, (int)ProductStatusAlias.Succeeded, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result.Success(
                Status("Succeeded", StatusCategory.Done, ProductStatusAlias.Succeeded)));

        _statusResolver
            .Setup(r => r.ForAlias(
                ProductWorkflowOwners.Deployment.Key, null, (int)ProductStatusAlias.Failed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result.Success(
                Status("Failed", StatusCategory.Removed, ProductStatusAlias.Failed)));
    }

    private RollBackDeploymentCommandHandler CreateSut() =>
        new(DbContext, _statusResolver.Object, CurrentUser.Object, Logger<RollBackDeploymentCommandHandler>(), DateTimeProvider.Object);

    private SucceedDeploymentCommandHandler SucceedSut() =>
        new(DbContext, _statusResolver.Object, CurrentUser.Object, Logger<SucceedDeploymentCommandHandler>(), DateTimeProvider.Object);

    private FailDeploymentCommandHandler FailSut() =>
        new(DbContext, _statusResolver.Object, CurrentUser.Object, Logger<FailDeploymentCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldRollBackASucceededDeployment()
    {
        // Arrange
        var deployment = SeedDeployment(EnvironmentCategory.Production);
        await SucceedSut().Handle(new SucceedDeploymentCommand(deployment.Id, null), TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new RollBackDeploymentCommand(deployment.Id, "Regression found.", Now.Plus(Duration.FromHours(2))),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        deployment.Outcome.Should().Be(ProductStatusAlias.RolledBack);
        deployment.IsChangeFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldRefuseRollingBackAFailedDeployment()
    {
        // Arrange
        var deployment = SeedDeployment();
        await FailSut().Handle(new FailDeploymentCommand(deployment.Id, null, null), TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new RollBackDeploymentCommand(deployment.Id, null, null), TestContext.Current.CancellationToken);

        // Assert
        // It never reached its environment, so counting it as a rollback would inflate change failure
        // rate by recording two failures for one attempt.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A failed deployment never reached its environment and cannot be rolled back.");
    }

    [Fact]
    public async Task Handle_ShouldRefuseRollingBackAnInFlightDeployment()
    {
        // Arrange
        var deployment = SeedDeployment();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new RollBackDeploymentCommand(deployment.Id, null, null), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A deployment that is still in progress cannot be rolled back. Record its outcome first.");
    }

    [Fact]
    public async Task Handle_ShouldRefuseARollbackBeforeCompletion()
    {
        // Arrange
        var deployment = SeedDeployment();
        await SucceedSut().Handle(
            new SucceedDeploymentCommand(deployment.Id, Now.Plus(Duration.FromHours(1))),
            TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new RollBackDeploymentCommand(deployment.Id, null, Now), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The rollback cannot be before the deployment completed.");
    }
}
