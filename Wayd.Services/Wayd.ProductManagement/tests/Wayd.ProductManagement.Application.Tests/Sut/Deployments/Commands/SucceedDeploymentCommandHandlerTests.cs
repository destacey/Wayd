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
using Wayd.Common.Domain.Events;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

// The delivery artifact record, not System.Version.
using Version = Wayd.ProductManagement.Domain.Models.Version;

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

        // What a production deployment moves an unreleased version or package to.
        foreach (var owner in new[] { ProductWorkflowOwners.Version.Key, ProductWorkflowOwners.ReleasePackage.Key })
        {
            _statusResolver
                .Setup(r => r.ForAlias(owner, null, (int)ProductStatusAlias.Released, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Result.Success(
                    Status("Released", StatusCategory.Done, ProductStatusAlias.Released)));
        }
    }

    private static readonly Instant CompletedAt = Now.Plus(Duration.FromMinutes(10));

    private Version VersionOf(Deployment deployment) => DbContext.Versions.Single(v => v.Id == deployment.VersionId);

    private SucceedDeploymentCommandHandler CreateSut() =>
        new(DbContext, _statusResolver.Object, CurrentUser.Object, CurrentPrincipal.Object, Logger<SucceedDeploymentCommandHandler>(), DateTimeProvider.Object);

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
    public async Task Handle_ShouldReleaseTheVersion_WhenProductionReceivedIt()
    {
        // Arrange
        var deployment = SeedDeployment(EnvironmentCategory.Production);
        var sut = CreateSut();

        // Act
        await sut.Handle(new SucceedDeploymentCommand(deployment.Id, CompletedAt), TestContext.Current.CancellationToken);

        // Assert — through MarkReleased, so the status and its history move as a person's would
        var version = VersionOf(deployment);
        version.ReleasedAt.Should().Be(CompletedAt);
        version.StatusCategory.Should().Be(StatusCategory.Done);
        version.StatusTransitions.Last().ActorEmployeeId.Should().Be(ActingEmployeeId);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldNotRelease_OutsideProduction()
    {
        // Arrange
        var deployment = SeedDeployment(EnvironmentCategory.Staging);
        var sut = CreateSut();

        // Act
        await sut.Handle(new SucceedDeploymentCommand(deployment.Id, CompletedAt), TestContext.Current.CancellationToken);

        // Assert
        VersionOf(deployment).ReleasedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldKeepAReleasedMomentAlreadyRecorded()
    {
        // Arrange — entered by hand before the deployment was recorded
        var deployment = SeedDeployment(EnvironmentCategory.Production);
        var version = VersionOf(deployment);
        var entered = Now.Minus(Duration.FromHours(3));
        version.MarkReleased(entered, Status("Released", StatusCategory.Done, ProductStatusAlias.Released), "Checkout", EventActor.System, Now);
        version.ClearDomainEvents();
        var sut = CreateSut();

        // Act
        await sut.Handle(new SucceedDeploymentCommand(deployment.Id, CompletedAt), TestContext.Current.CancellationToken);

        // Assert
        version.ReleasedAt.Should().Be(entered);
        version.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldLeaveAWithdrawnVersionAlone()
    {
        // Arrange
        var deployment = SeedDeployment(EnvironmentCategory.Production);
        var version = VersionOf(deployment);
        version.Withdraw(null, Status("Withdrawn", StatusCategory.Removed, ProductStatusAlias.Withdrawn), "Checkout", EventActor.System, Now);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new SucceedDeploymentCommand(deployment.Id, CompletedAt), TestContext.Current.CancellationToken);

        // Assert — the deployment still succeeds; only the release is skipped
        result.IsSuccess.Should().BeTrue();
        version.ReleasedAt.Should().BeNull();
        version.StatusCategory.Should().Be(StatusCategory.Removed);
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
