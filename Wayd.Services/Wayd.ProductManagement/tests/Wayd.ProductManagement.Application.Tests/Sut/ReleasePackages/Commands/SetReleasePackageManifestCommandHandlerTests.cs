using FluentAssertions;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.ReleasePackages.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.ReleasePackages.Commands;

/// <summary>
/// Amending what a package claims to have shipped.
/// </summary>
public sealed class SetReleasePackageManifestCommandHandlerTests : ProductCommandTestBase
{
    private SetReleasePackageManifestCommandHandler CreateSut() =>
        new(DbContext, CurrentUser.Object, Logger<SetReleasePackageManifestCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldReplaceTheManifest()
    {
        // Arrange
        var product = SeedProduct();
        var other = SeedProduct("Payments");
        var package = SeedReleasePackage(product.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new SetReleasePackageManifestCommand(
                package.Id,
                [new ManifestEntry(other.Id, null, "2.0", ManifestEntryKind.Changed)]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        package.Components.Should().ContainSingle();
        package.Components.Single().ProductId.Should().Be(other.Id);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenAComponentProductDoesNotExist()
    {
        // A manifest naming a product that does not exist records an untraceable shipment.
        // Arrange
        var product = SeedProduct();
        var package = SeedReleasePackage(product.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new SetReleasePackageManifestCommand(
                package.Id,
                [new ManifestEntry(Guid.CreateVersion7(), null, "2.0", ManifestEntryKind.Changed)]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenThePackageDoesNotExist()
    {
        // Arrange
        var product = SeedProduct();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new SetReleasePackageManifestCommand(
                Guid.CreateVersion7(),
                [new ManifestEntry(product.Id, null, "2.0", ManifestEntryKind.Changed)]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Release package not found.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenThePackageIsWithdrawn()
    {
        // Arrange
        var product = SeedProduct();
        var package = SeedReleasePackage(
            product.Id,
            status: Status("Withdrawn", StatusCategory.Removed, ProductStatusAlias.Withdrawn));
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new SetReleasePackageManifestCommand(
                package.Id,
                [new ManifestEntry(product.Id, null, "2.0", ManifestEntryKind.Changed)]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A withdrawn package's manifest cannot be amended.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
