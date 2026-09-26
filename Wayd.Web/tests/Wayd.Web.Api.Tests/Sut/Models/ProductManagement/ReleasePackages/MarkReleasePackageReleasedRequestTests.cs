using FluentAssertions;
using FluentValidation.TestHelper;
using NodaTime;
using Wayd.Web.Api.Models.ProductManagement.ReleasePackages;

namespace Wayd.Web.Api.Tests.Sut.Models.ProductManagement.ReleasePackages;

/// <summary>
/// The mark-package-released request, which still accepts the deprecated ReleasedDate from before ReleasedAt was an instant.
/// </summary>
#pragma warning disable CS0618 // The deprecated field is what these cover.
public sealed class MarkReleasePackageReleasedRequestTests
{
    private static readonly DateTimeZone Chicago = DateTimeZoneProviders.Tzdb["America/Chicago"];

    private readonly MarkReleasePackageReleasedRequestValidator _validator = new();

    [Fact]
    public void ResolveReleasedAt_UsesTheInstantWhenOneIsSent()
    {
        // Arrange
        var sent = Instant.FromUtc(2026, 9, 18, 2, 30);
        var request = new MarkReleasePackageReleasedRequest { ReleasedAt = sent };

        // Act
        var resolved = request.ResolveReleasedAt(Chicago);

        // Assert
        resolved.Should().Be(sent);
        request.UsesLegacyDates().Should().BeFalse();
    }

    [Fact]
    public void ResolveReleasedAt_ReadsTheDeprecatedDateAsNoonInTheOrganizationsZone()
    {
        // Arrange — noon in Chicago in September is 17:00 UTC
        var request = new MarkReleasePackageReleasedRequest { ReleasedDate = new LocalDate(2026, 9, 18) };

        // Act
        var resolved = request.ResolveReleasedAt(Chicago);

        // Assert
        resolved.Should().Be(Instant.FromUtc(2026, 9, 18, 17, 0));
        request.UsesLegacyDates().Should().BeTrue();
    }

    [Fact]
    public void Validate_RequiresOneOfThem()
    {
        // Act
        var result = _validator.TestValidate(new MarkReleasePackageReleasedRequest());

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorMessage == "ReleasedAt is required.");
    }

    [Fact]
    public void Validate_RefusesBoth()
    {
        // Arrange — which one to believe is a guess, so neither is taken
        var request = new MarkReleasePackageReleasedRequest { ReleasedAt = Instant.FromUtc(2026, 9, 18, 2, 30), ReleasedDate = new LocalDate(2026, 9, 18) };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.Errors.Should().ContainSingle(e => e.ErrorMessage == "Send ReleasedAt or the deprecated ReleasedDate, not both.");
    }
}
#pragma warning restore CS0618