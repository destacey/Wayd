using FluentAssertions;
using FluentValidation.TestHelper;
using NodaTime;
using Wayd.Web.Api.Models.ProductManagement.ReleasePackages;

namespace Wayd.Web.Api.Tests.Sut.Models.ProductManagement.ReleasePackages;

/// <summary>
/// A release package CSV row. Its released moment is an instant, read only from a timestamp that
/// carries its offset.
/// </summary>
public sealed class ImportReleasePackageRequestTests
{
    private readonly ImportReleasePackageRequestValidator _validator = new();

    private static ImportReleasePackageRequest Row(string? releasedAt = null) =>
        new()
        {
            Version = "WAYD-2026.09",
            ReleasedAt = releasedAt,
        };

    [Fact]
    public void ToImportReleasePackageDto_ReadsTheReleasedMomentAsTheInstantItsOffsetDescribes()
    {
        // Arrange
        var request = Row(releasedAt: "2026-09-17T21:30:00-05:00");

        // Act
        var dto = request.ToImportReleasePackageDto([]);

        // Assert
        dto.ReleasedAt.Should().Be(Instant.FromUtc(2026, 9, 18, 2, 30));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    [InlineData("2026-09-18T02:30:00Z")]
    public void Validate_AcceptsABlankOrOffsetReleasedMoment(string? value)
    {
        // Act
        var result = _validator.TestValidate(Row(value));

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("2026-09-18")]
    [InlineData("2026-09-18T02:30:00")]
    public void Validate_RefusesAReleasedMomentWithoutAnOffset(string value)
    {
        // Act
        var result = _validator.TestValidate(Row(value));

        // Assert
        result.ShouldHaveValidationErrorFor(p => p.ReleasedAt);
    }
}
