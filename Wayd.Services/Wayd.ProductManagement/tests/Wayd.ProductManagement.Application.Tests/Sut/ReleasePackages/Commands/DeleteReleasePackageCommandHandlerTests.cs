using FluentAssertions;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.ProductManagement.Application.ReleasePackages.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Tests.Sut.ReleasePackages.Commands;

/// <summary>
/// Deleting a package, which takes its deployments along and drops it from any release listing it.
/// </summary>
public sealed class DeleteReleasePackageCommandHandlerTests : ProductCommandTestBase
{
    private DeleteReleasePackageCommandHandler CreateSut() =>
        new(DbContext, DbContext, CurrentUser.Object, CurrentPrincipal.Object, Logger<DeleteReleasePackageCommandHandler>(), DateTimeProvider.Object);

    private ReleasePackage SeedPackageWithHistory()
    {
        var product = SeedProduct($"product-{Guid.CreateVersion7()}"[..16]);
        var package = SeedReleasePackage(product.Id);
        DbContext.AddStatusTransitions(package.DrainStatusTransitions());

        return package;
    }

    [Fact]
    public async Task Handle_ShouldDeleteThePackageAndItsStatusHistory()
    {
        // Arrange
        var package = SeedPackageWithHistory();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DeleteReleasePackageCommand(package.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        DbContext.ReleasePackages.Should().BeEmpty();
        DbContext.StatusTransitions.Should().NotContain(t => t.RecordId == package.Id);
        package.DomainEvents.OfType<PackageDeletedEvent>().Should().ContainSingle();
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldDeleteEveryDeploymentOfIt()
    {
        // Arrange
        var package = SeedPackageWithHistory();
        var deployment = SeedDeployment(package: package);
        var elsewhere = SeedDeployment();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DeleteReleasePackageCommand(package.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        DbContext.Deployments.Should().ContainSingle().Which.Id.Should().Be(elsewhere.Id);
        deployment.DomainEvents.OfType<DeploymentDeletedEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhileAReleaseListsIt()
    {
        // Arrange — a delete never edits a release's contents behind its back.
        var package = SeedPackageWithHistory();
        var release = SeedRelease();
        release.SetContents([], [package.Id], [], EventActor.System, Now);
        foreach (var inclusion in release.Packages)
        {
            DbContext.AddReleasePackageInclusion(inclusion);
        }

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DeleteReleasePackageCommand(package.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This package is listed in 1 release(s). Remove it from them, or delete them, first.");
        DbContext.ReleasePackages.Should().ContainSingle();
        release.Packages.Should().ContainSingle();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenThePackageDoesNotExist()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new DeleteReleasePackageCommand(Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Release package not found.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
