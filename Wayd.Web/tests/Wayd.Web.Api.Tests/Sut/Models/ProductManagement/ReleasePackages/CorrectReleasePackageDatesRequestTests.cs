using FluentAssertions;
using FluentValidation.TestHelper;
using NodaTime;
using Wayd.Web.Api.Models.ProductManagement.ReleasePackages;

namespace Wayd.Web.Api.Tests.Sut.Models.ProductManagement.ReleasePackages;

/// <summary>
/// The package date correction, which still accepts the deprecated ReleasedDate.
/// </summary>
#pragma warning disable CS0618 // The deprecated field is what these cover.
public sealed class CorrectReleasePackageDatesRequestTests
{
    private static readonly DateTimeZone Chicago = DateTimeZoneProviders.Tzdb["America/Chicago"];

    private readonly CorrectReleasePackageDatesRequestValidator _validator = new();

    [Fact]
    public void ResolveReleasedAt_ReadsTheDeprecatedDateAsNoonInTheOrganizationsZone()
    {
        // Arrange
        var request = new CorrectReleasePackageDatesRequest { ReleasedDate = new LocalDate(2026, 9, 18) };

        // Act
        var resolved = request.ResolveReleasedAt(Chicago);

        // Assert
        resolved.Should().Be(Instant.FromUtc(2026, 9, 18, 17, 0));
    }

    [Fact]
    public void Validate_RefusesBoth()
    {
        // Arrange
        var request = new CorrectReleasePackageDatesRequest
        {
            ReleasedAt = Instant.FromUtc(2026, 9, 18, 2, 30),
            ReleasedDate = new LocalDate(2026, 9, 18),
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.Errors.Should().ContainSingle(e => e.ErrorMessage == "Send ReleasedAt or the deprecated ReleasedDate, not both.");
    }
}
#pragma warning restore CS0618