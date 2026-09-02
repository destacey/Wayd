using FluentAssertions;
using Wayd.ProductManagement.Application.Releases.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.Releases.Commands;

/// <summary>
/// Setting the versions a release carries directly.
/// </summary>
/// <remarks>
/// The handler resolves every version reachable through the release's packages, because the aggregate
/// holds ids and cannot load a manifest. That resolution is the whole point of these tests: without it
/// the double-count rule silently never fires.
/// </remarks>
public sealed class SetReleaseVersionsCommandHandlerTests : ProductCommandTestBase
{
    private SetReleaseVersionsCommandHandler CreateSut() =>
        new(DbContext, CurrentUser.Object, CurrentPrincipal.Object,
            Logger<SetReleaseVersionsCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldCarryTheVersions()
    {
        // Arrange
        var product = SeedProduct();
        var version = SeedVersion(product.Id);
        var release = SeedRelease();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new SetReleaseVersionsCommand(release.Id, [version.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        release.Versions.Select(v => v.VersionId).Should().Equal(version.Id);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheVersionDoesNotExist()
    {
        // Arrange
        var release = SeedRelease();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new SetReleaseVersionsCommand(release.Id, [Guid.CreateVersion7()]),
            TestContext.Current.CancellationToken);

        // Assert
        // Checked rather than left to the foreign key, so a typo reads as a refusal naming the problem
        // rather than a constraint violation at save time.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The release names a version that does not exist.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheVersionAlreadyShipsInOneOfTheReleasesPackages()
    {
        // Arrange
        // The version is in a package, and that package is already on the release — so carrying the
        // version directly would announce the same shipment twice.
        var product = SeedProduct();
        var version = SeedVersion(product.Id);
        var package = SeedReleasePackage(product.Id, versionId: version.Id);
        var release = SeedRelease();

        await new SetReleasePackagesCommandHandler(
                DbContext, CurrentUser.Object, CurrentPrincipal.Object,
                Logger<SetReleasePackagesCommandHandler>(), DateTimeProvider.Object)
            .Handle(new SetReleasePackagesCommand(release.Id, [package.Id]), TestContext.Current.CancellationToken);

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new SetReleaseVersionsCommand(release.Id, [version.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot also be carried directly");
        release.Versions.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldAllowTheVersion_WhenItsPackageIsNotOnThisRelease()
    {
        // Arrange
        // The same version sits in a package, but that package is not part of this release — so there
        // is nothing to double-count and carrying it directly is legitimate.
        var product = SeedProduct();
        var version = SeedVersion(product.Id);
        SeedReleasePackage(product.Id, versionId: version.Id);
        var release = SeedRelease();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new SetReleaseVersionsCommand(release.Id, [version.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        release.Versions.Select(v => v.VersionId).Should().Equal(version.Id);
    }

    [Fact]
    public async Task Handle_ShouldRemoveEveryVersion_WhenSentAnEmptyList()
    {
        // Arrange
        var product = SeedProduct();
        var version = SeedVersion(product.Id);
        var release = SeedRelease();
        await CreateSut().Handle(
            new SetReleaseVersionsCommand(release.Id, [version.Id]), TestContext.Current.CancellationToken);

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new SetReleaseVersionsCommand(release.Id, []), TestContext.Current.CancellationToken);

        // Assert
        // Whole-set replacement: an omitted version is a removed version.
        result.IsSuccess.Should().BeTrue();
        release.Versions.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheReleaseDoesNotExist()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new SetReleaseVersionsCommand(Guid.CreateVersion7(), []), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Release not found.");
    }
}
