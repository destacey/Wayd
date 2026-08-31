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
/// Recording that a deployment did not reach its environment.
/// </summary>
public sealed class FailDeploymentCommandHandlerTests : ProductCommandTestBase
{
    private readonly Mock<IStatusResolver> _statusResolver = new();

    public FailDeploymentCommandHandlerTests()
    {
        _statusResolver
            .Setup(r => r.ForAlias(
                ProductWorkflowOwners.Deployment.Key, null, (int)ProductStatusAlias.Failed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result.Success(
                Status("Failed", StatusCategory.Removed, ProductStatusAlias.Failed)));
    }

    private FailDeploymentCommandHandler CreateSut() =>
        new(DbContext, _statusResolver.Object, CurrentUser.Object, Logger<FailDeploymentCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldRecordTheFailureAndReason()
    {
        // Arrange
        var deployment = SeedDeployment();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new FailDeploymentCommand(deployment.Id, "Migration timed out.", null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        deployment.Outcome.Should().Be(ProductStatusAlias.Failed);
        deployment.Reason.Should().Be("Migration timed out.");
    }

    [Fact]
    public async Task Handle_ShouldCountAsAChangeFailure_InProduction()
    {
        // Arrange
        var deployment = SeedDeployment(EnvironmentCategory.Production);
        var sut = CreateSut();

        // Act
        await sut.Handle(new FailDeploymentCommand(deployment.Id, null, null), TestContext.Current.CancellationToken);

        // Assert
        deployment.IsChangeFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldNotCountAsAChangeFailure_BeforeProduction()
    {
        // Arrange
        var deployment = SeedDeployment(EnvironmentCategory.Staging);
        var sut = CreateSut();

        // Act
        await sut.Handle(new FailDeploymentCommand(deployment.Id, null, null), TestContext.Current.CancellationToken);

        // Assert
        // A failure caught before production is a failure that was prevented; counting it would invert
        // what the measure means.
        deployment.Outcome.Should().Be(ProductStatusAlias.Failed);
        deployment.IsChangeFailure.Should().BeFalse();
    }
}
