using FluentAssertions;
using Wayd.Common.Application.Dtos;
using Wayd.ProductManagement.Application.Releases.Dtos;
using Wayd.Web.Api.Models.ProductManagement.Releases;
using Wayd.Web.Api.Tests.Infrastructure;

namespace Wayd.Web.Api.Tests.Sut.Models.ProductManagement.Releases;

/// <summary>
/// The release contents request as the contents patch endpoint builds it from the current release.
/// </summary>
public sealed class SetReleaseContentsRequestTests
{
    private static ReleaseDto FullyPopulatedRelease() => new()
    {
        Id = Guid.NewGuid(),
        Key = 9,
        Version = "2026.09",
        Versions =
        [
            new ReleaseVersionDto { Version = NavigationDto.Create(Guid.NewGuid(), 31, "4.10.0") },
        ],
        Packages =
        [
            new ReleasePackageSummaryDto { Package = NavigationDto.Create(Guid.NewGuid(), 41, "2026.09.1") },
            new ReleasePackageSummaryDto { Package = NavigationDto.Create(Guid.NewGuid(), 42, "2026.09.2") },
        ],
    };

    [Fact]
    public void FromDto_SetsEveryPropertyFromAFullyPopulatedRelease()
    {
        // Arrange
        var release = FullyPopulatedRelease();

        // Act
        var request = SetReleaseContentsRequest.FromDto(release);

        // Assert
        PatchRequestAssertions.ShouldSetEveryProperty(request);
    }

    [Fact]
    public void FromDto_TakesTheIdsOfTheVersionsAndPackages()
    {
        // Arrange
        var release = FullyPopulatedRelease();

        // Act
        var request = SetReleaseContentsRequest.FromDto(release);

        // Assert
        request.VersionIds.Should().Equal(release.Versions.Select(v => v.Version.Id));
        request.PackageIds.Should().Equal(release.Packages.Select(p => p.Package.Id));
    }
}
