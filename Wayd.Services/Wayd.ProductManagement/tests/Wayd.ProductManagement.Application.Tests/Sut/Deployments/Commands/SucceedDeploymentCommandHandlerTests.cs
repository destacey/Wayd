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
/// Recording that a deployment reached its environment.
/// </summary>
public sealed class SucceedDeploymentCommandHandlerTests : ProductCommandTestBase
{
    private readonly Mock<IStatusResolver> _statusResolver = new();

    public SucceedDeploymentCommandHandlerTests()
    {
        _statusResolver
            .Setup(r => r.ForAlias(
                ProductWorkflowOwners.Deployment.Key, null, (int)ProductStatusAlias.Succeeded, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result.Success(
                Status("Succeeded", StatusCategory.Done, ProductStatusAlias.Succeeded)));
    }

    private SucceedDeploymentCommandHandler CreateSut() =>
        new(DbContext, _statusResolver.Object, CurrentUser.Object, Logger<SucceedDeploymentCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldCompleteTheDeployment()
    {
        // Arrange
        var deployment = SeedDeployment();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new SucceedDeploymentCommand(deployment.Id, Now.Plus(Duration.FromMinutes(10))),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        deployment.IsComplete.Should().BeTrue();
        deployment.Outcome.Should().Be(ProductStatusAlias.Succeeded);
    }

    [Fact]
    public async Task Handle_ShouldNotCountAsAChangeFailure()
    {
        // Arrange
        var deployment = SeedDeployment(EnvironmentCategory.Production);
        var sut = CreateSut();

        // Act
        await sut.Handle(new SucceedDeploymentCommand(deployment.Id, null), TestContext.Current.CancellationToken);

        // Assert
        deployment.IsChangeFailure.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldRefuseCompletingTwice()
    {
        // Arrange
        var deployment = SeedDeployment();
        var sut = CreateSut();
        await sut.Handle(new SucceedDeploymentCommand(deployment.Id, null), TestContext.Current.CancellationToken);

        // Act
        var result = await sut.Handle(
            new SucceedDeploymentCommand(deployment.Id, null), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
    }
}
