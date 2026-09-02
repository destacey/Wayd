using FluentAssertions;
using Wayd.ProductManagement.Application.Releases.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.Releases.Commands;

/// <summary>
/// Setting what a release announces — its packages and the versions it carries directly.
/// </summary>
/// <remarks>
/// The handler resolves every version reachable through the supplied packages, because the aggregate
/// holds ids and cannot load a manifest. That resolution is the whole point of these tests: without it
/// the double-count rule silently never fires. It is resolved from the packages in the request rather
/// than the ones already attached, which is what lets a version move between the two routes in one
/// call.
/// </remarks>
public sealed class SetReleaseContentsCommandHandlerTests : ProductCommandTestBase
{
    private SetReleaseContentsCommandHandler CreateSut() =>
        new(DbContext, CurrentUser.Object, CurrentPrincipal.Object,
            Logger<SetReleaseContentsCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldSetBothRoutes()
    {
        // Arrange
        var product = SeedProduct();
        var version = SeedVersion(product.Id);
        var package = SeedReleasePackage(product.Id, version: "2026.14");
        var release = SeedRelease();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new SetReleaseContentsCommand(release.Id, [version.Id], [package.Id]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        release.Versions.Select(v => v.VersionId).Should().Equal(version.Id);
        release.Packages.Select(p => p.PackageId).Should().Equal(package.Id);
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
            new SetReleaseContentsCommand(release.Id, [Guid.CreateVersion7()], []),
            TestContext.Current.CancellationToken);

        // Assert
        // Checked rather than left to the foreign key, so a typo reads as a refusal naming the problem
        // rather than a constraint violation at save time.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The release names a version that does not exist.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenThePackageDoesNotExist()
    {
        // Arrange
        var release = SeedRelease();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new SetReleaseContentsCommand(release.Id, [], [Guid.CreateVersion7()]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The release names a package that does not exist.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenAVersionAlsoShipsInOneOfTheSuppliedPackages()
    {
        // Arrange
        // The version ships inside the package, so announcing both would announce the same shipment
        // twice.
        var product = SeedProduct();
        var version = SeedVersion(product.Id);
        var package = SeedReleasePackage(product.Id, versionId: version.Id);
        var release = SeedRelease();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new SetReleaseContentsCommand(release.Id, [version.Id], [package.Id]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot also be carried directly");
        release.Versions.Should().BeEmpty();
        release.Packages.Should().BeEmpty();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldAllowMovingAVersionIntoThePackageThatCarriesIt()
    {
        // Arrange
        // The release carries the version directly; the same version also ships inside a package that
        // is about to be added in its place.
        var product = SeedProduct();
        var version = SeedVersion(product.Id);
        var package = SeedReleasePackage(product.Id, versionId: version.Id);
        var release = SeedRelease();

        await CreateSut().Handle(
            new SetReleaseContentsCommand(release.Id, [version.Id], []),
            TestContext.Current.CancellationToken);

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new SetReleaseContentsCommand(release.Id, [], [package.Id]),
            TestContext.Current.CancellationToken);

        // Assert
        // One statement of intent, so the rule is judged against what the release ends up containing.
        // Split across two calls this was reachable only by removing the version first.
        result.IsSuccess.Should().BeTrue();
        release.Versions.Should().BeEmpty();
        release.Packages.Select(p => p.PackageId).Should().Equal(package.Id);
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
            new SetReleaseContentsCommand(release.Id, [version.Id], []),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        release.Versions.Select(v => v.VersionId).Should().Equal(version.Id);
    }

    [Fact]
    public async Task Handle_ShouldAllowTheVersion_WhenThePackageLineNamesNoVersionRecord()
    {
        // Arrange
        // A carried-forward manifest line holding only a version string covers no version record, so it
        // cannot conflict with one carried directly.
        var product = SeedProduct();
        var version = SeedVersion(product.Id);
        var package = SeedReleasePackage(product.Id, versionId: null);
        var release = SeedRelease();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new SetReleaseContentsCommand(release.Id, [version.Id], [package.Id]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        release.Versions.Select(v => v.VersionId).Should().Equal(version.Id);
        release.Packages.Select(p => p.PackageId).Should().Equal(package.Id);
    }

    [Fact]
    public async Task Handle_ShouldClearTheRelease_WhenSentTwoEmptyLists()
    {
        // Arrange
        var product = SeedProduct();
        var version = SeedVersion(product.Id);
        var package = SeedReleasePackage(product.Id, version: "2026.14");
        var release = SeedRelease();
        await CreateSut().Handle(
            new SetReleaseContentsCommand(release.Id, [version.Id], [package.Id]),
            TestContext.Current.CancellationToken);

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new SetReleaseContentsCommand(release.Id, [], []), TestContext.Current.CancellationToken);

        // Assert
        // Whole-set replacement: an omitted entry is a removed entry, and an empty release is a
        // legitimate state rather than a draft.
        result.IsSuccess.Should().BeTrue();
        release.Versions.Should().BeEmpty();
        release.Packages.Should().BeEmpty();
        release.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheReleaseDoesNotExist()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new SetReleaseContentsCommand(Guid.CreateVersion7(), [], []),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Release not found.");
    }
}
