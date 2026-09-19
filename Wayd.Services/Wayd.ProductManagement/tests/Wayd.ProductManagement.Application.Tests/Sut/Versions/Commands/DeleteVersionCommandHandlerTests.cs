using FluentAssertions;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Application.Versions.Commands;

namespace Wayd.ProductManagement.Application.Tests.Sut.Versions.Commands;

/// <summary>
/// Deleting a version, which takes its deployments along and is refused while anything else names it.
/// </summary>
public sealed class DeleteVersionCommandHandlerTests : ProductCommandTestBase
{
    private DeleteVersionCommandHandler CreateSut() =>
        new(DbContext, DbContext, CurrentUser.Object, CurrentPrincipal.Object, Logger<DeleteVersionCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldDeleteTheVersionItsHistoryAndItsDeployments()
    {
        // Arrange
        var deployment = SeedDeployment();
        var version = DbContext.Versions.Single(v => v.Id == deployment.VersionId);
        DbContext.AddStatusTransitions(version.DrainStatusTransitions());
        DbContext.AddStatusTransitions(deployment.DrainStatusTransitions());
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DeleteVersionCommand(version.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        DbContext.Versions.Should().NotContain(v => v.Id == version.Id);
        DbContext.Deployments.Should().BeEmpty();
        DbContext.StatusTransitions.Should().BeEmpty();
        version.DomainEvents.OfType<VersionDeletedEvent>().Should().ContainSingle()
            .Which.Number.Should().Be(version.Number);
        deployment.DomainEvents.OfType<DeploymentDeletedEvent>().Should().ContainSingle();
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhileAReleaseListsIt()
    {
        // Arrange
        var product = SeedProduct();
        var version = SeedVersion(product.Id);
        var release = SeedRelease();
        release.SetContents([version.Id], [], [], EventActor.System, Now);
        foreach (var listed in release.Versions)
        {
            DbContext.AddReleaseVersion(listed);
        }

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DeleteVersionCommand(version.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This version is listed in 1 release(s). Remove it from them, or delete them, first.");
        DbContext.Versions.Should().Contain(v => v.Id == version.Id);
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhileAPackageManifestNamesIt()
    {
        // Arrange — the manifest line has no foreign key, so only this check stops a dangling reference.
        var product = SeedProduct();
        var version = SeedVersion(product.Id);
        SeedReleasePackage(product.Id, versionId: version.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DeleteVersionCommand(version.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This version is named in the manifest of 1 package(s). Remove it from them, or delete them, first.");
        DbContext.Versions.Should().Contain(v => v.Id == version.Id);
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheVersionDoesNotExist()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DeleteVersionCommand(Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Version not found.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
