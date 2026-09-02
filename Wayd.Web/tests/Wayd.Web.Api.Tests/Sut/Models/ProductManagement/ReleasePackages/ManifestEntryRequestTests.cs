using FluentAssertions;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Web.Api.Models.ProductManagement.ReleasePackages;

namespace Wayd.Web.Api.Tests.Sut.Models.ProductManagement.ReleasePackages;

/// <summary>
/// The manifest line an API caller sends, and what it becomes.
/// </summary>
/// <remarks>
/// The version link is the field the release double-count rule reads: a manifest line naming a version
/// record is what makes that version "already shipping inside a package". This was named
/// <c>ReleaseId</c> after Release and Version were split, so a caller sending <c>versionId</c> bound
/// nothing and every line recorded a null version record — the rule then never fired, and the release
/// contents editor could not tell a covered version from an uncovered one.
///
/// Nothing failed loudly: the model bound, validation passed, the package saved. Only the link was
/// missing. These tests pin the mapping so a rename cannot silently drop it again.
/// </remarks>
public sealed class ManifestEntryRequestTests
{
    [Fact]
    public void ToManifestEntry_ShouldCarryTheVersionRecordThrough()
    {
        // Arrange
        var productId = Guid.CreateVersion7();
        var versionId = Guid.CreateVersion7();
        var request = new ManifestEntryRequest
        {
            ProductId = productId,
            VersionId = versionId,
            Version = "4.10.0",
            Kind = ManifestEntryKind.Changed,
        };

        // Act
        var entry = request.ToManifestEntry();

        // Assert
        entry.ProductId.Should().Be(productId);
        entry.VersionId.Should().Be(versionId);
        entry.Version.Should().Be("4.10.0");
        entry.Kind.Should().Be(ManifestEntryKind.Changed);
    }

    [Fact]
    public void ToManifestEntry_ShouldAllowNoVersionRecord()
    {
        // Arrange
        // A carried-forward component often names a version string that was never cut in Wayd, so the
        // line records the text without pointing at a version record.
        var request = new ManifestEntryRequest
        {
            ProductId = Guid.CreateVersion7(),
            VersionId = null,
            Version = "1.1.0",
            Kind = ManifestEntryKind.CarriedForward,
        };

        // Act
        var entry = request.ToManifestEntry();

        // Assert
        entry.VersionId.Should().BeNull();
        entry.Version.Should().Be("1.1.0");
    }
}
