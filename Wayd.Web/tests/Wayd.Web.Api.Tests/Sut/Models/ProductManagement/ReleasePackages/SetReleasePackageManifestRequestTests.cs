using FluentAssertions;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application.ReleasePackages.Dtos;
using Wayd.Web.Api.Models.ProductManagement.ReleasePackages;
using Wayd.Web.Api.Tests.Infrastructure;

namespace Wayd.Web.Api.Tests.Sut.Models.ProductManagement.ReleasePackages;

/// <summary>
/// The manifest request as the manifest patch endpoint builds it from the current package.
/// </summary>
public sealed class SetReleasePackageManifestRequestTests
{
    private static ReleasePackageDto FullyPopulatedPackage() => new()
    {
        Id = Guid.NewGuid(),
        Key = 41,
        Version = "2026.09.1",
        Components =
        [
            new ReleasePackageComponentDto
            {
                Product = NavigationDto.Create(Guid.NewGuid(), 5, "Billing"),
                VersionRecord = NavigationDto.Create(Guid.NewGuid(), 31, "4.10.0"),
                Version = "4.10.0",
                Kind = ManifestEntryKind.Changed,
            },
            new ReleasePackageComponentDto
            {
                Product = NavigationDto.Create(Guid.NewGuid(), 6, "Ledger"),
                VersionRecord = NavigationDto.Create(Guid.NewGuid(), 32, "2.3.1"),
                Version = "2.3.1",
                Kind = ManifestEntryKind.CarriedForward,
            },
        ],
    };

    [Fact]
    public void FromDto_SetsEveryPropertyOfTheRequestAndEachLine()
    {
        // Arrange
        var package = FullyPopulatedPackage();

        // Act
        var request = SetReleasePackageManifestRequest.FromDto(package);

        // Assert
        PatchRequestAssertions.ShouldSetEveryProperty(request);
        foreach (var line in request.Components)
            PatchRequestAssertions.ShouldSetEveryProperty(line);
    }

    [Fact]
    public void FromDto_MapsEachComponentToAManifestLine()
    {
        // Arrange
        var package = FullyPopulatedPackage();

        // Act
        var request = SetReleasePackageManifestRequest.FromDto(package);

        // Assert
        request.Components.Select(c => (c.ProductId, c.VersionId, c.Version, c.Kind)).Should().Equal(
            package.Components.Select(c => (c.Product.Id, (Guid?)c.VersionRecord!.Id, c.Version, c.Kind)));
    }

    [Fact]
    public void FromDto_LeavesTheVersionRecordEmptyForALineThatNamesNone()
    {
        // Arrange
        var package = FullyPopulatedPackage();
        var line = package.Components.First() with { VersionRecord = null };
        package = package with { Components = [line] };

        // Act
        var request = SetReleasePackageManifestRequest.FromDto(package);

        // Assert
        request.Components.Single().VersionId.Should().BeNull();
    }
}
