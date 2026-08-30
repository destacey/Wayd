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
/// Starting a deployment. The handler freezes the environment's category onto the record, because a
/// later reclassification must not rewrite what past deployments counted as.
/// </summary>
public sealed class StartDeploymentCommandHandlerTests : ProductCommandTestBase
{
    private readonly Mock<IStatusResolver> _statusResolver = new();

    public StartDeploymentCommandHandlerTests()
    {
        _statusResolver
            .Setup(r => r.ForAlias(
                ProductWorkflowOwners.Deployment.Key, null, (int)ProductStatusAlias.InProgress, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result.Success(
                Status("In Progress", StatusCategory.Active, ProductStatusAlias.InProgress)));
    }

    private StartDeploymentCommandHandler CreateSut() =>
        new(DbContext, _statusResolver.Object, CurrentUser.Object, Logger<StartDeploymentCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldStartTheDeployment()
    {
        // Arrange
        var environment = SeedEnvironment();
        var product = SeedProduct();
        var release = SeedRelease(product.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new StartDeploymentCommand(release.Id, null, environment.Id, "4.8.2.008", null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var deployment = DbContext.Deployments.Should().ContainSingle().Subject;
        deployment.ReleaseId.Should().Be(release.Id);
        deployment.ArtifactId.Should().Be("4.8.2.008");
    }

    [Fact]
    public async Task Handle_ShouldFreezeTheEnvironmentCategory()
    {
        // Arrange
        var environment = SeedEnvironment("prod", EnvironmentCategory.Production, 3);
        var product = SeedProduct();
        var release = SeedRelease(product.Id);
        var sut = CreateSut();

        // Act
        await sut.Handle(
            new StartDeploymentCommand(release.Id, null, environment.Id, null, null),
            TestContext.Current.CancellationToken);

        // Assert
        // Copied onto the record, not resolved through the environment on read — reclassifying the
        // environment later cannot retroactively change what this counted as.
        DbContext.Deployments.Single().EnvironmentCategory.Should().Be(EnvironmentCategory.Production);
    }

    [Fact]
    public async Task Handle_ShouldRefuseBothAReleaseAndAPackage()
    {
        // Arrange
        var environment = SeedEnvironment();
        var product = SeedProduct();
        var release = SeedRelease(product.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new StartDeploymentCommand(release.Id, Guid.CreateVersion7(), environment.Id, null, null),
            TestContext.Current.CancellationToken);

        // Assert
        // Where a package exists it is the unit that shipped, so counting both would double-count one
        // pipeline run.
        result.IsFailure.Should().BeTrue();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldRefuseNeitherAReleaseNorAPackage()
    {
        // Arrange
        var environment = SeedEnvironment();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new StartDeploymentCommand(null, null, environment.Id, null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A deployment must be for either a release or a package.");
    }

    [Fact]
    public async Task Handle_ShouldRefuseAnInactiveEnvironment()
    {
        // Arrange
        var environment = SeedEnvironment();
        environment.Deactivate(Wayd.Common.Domain.Events.EventActor.System, Now);
        var product = SeedProduct();
        var release = SeedRelease(product.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new StartDeploymentCommand(release.Id, null, environment.Id, null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("inactive");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheReleaseDoesNotExist()
    {
        // Arrange
        var environment = SeedEnvironment();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new StartDeploymentCommand(Guid.CreateVersion7(), null, environment.Id, null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Release not found.");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheEnvironmentDoesNotExist()
    {
        // Arrange
        var product = SeedProduct();
        var release = SeedRelease(product.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new StartDeploymentCommand(release.Id, null, Guid.CreateVersion7(), null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Deployment environment not found.");
    }
}
