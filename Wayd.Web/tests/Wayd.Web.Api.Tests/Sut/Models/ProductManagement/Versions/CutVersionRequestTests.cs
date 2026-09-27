using FluentAssertions;
using FluentValidation.TestHelper;
using NodaTime;
using Wayd.Web.Api.Models.ProductManagement.Versions;

namespace Wayd.Web.Api.Tests.Sut.Models.ProductManagement.Versions;

/// <summary>
/// The cut-version request, which still accepts the deprecated CutDate from before CutAt was an instant.
/// </summary>
#pragma warning disable CS0618 // The deprecated field is what these cover.
public sealed class CutVersionRequestTests
{
    private static readonly DateTimeZone Chicago = DateTimeZoneProviders.Tzdb["America/Chicago"];

    private readonly CutVersionRequestValidator _validator = new();

    [Fact]
    public void ResolveCutAt_UsesTheInstantWhenOneIsSent()
    {
        // Arrange
        var sent = Instant.FromUtc(2026, 9, 18, 2, 30);
        var request = new CutVersionRequest { CutAt = sent };

        // Act
        var resolved = request.ResolveCutAt(Chicago);

        // Assert
        resolved.Should().Be(sent);
        request.UsesLegacyDates().Should().BeFalse();
    }

    [Fact]
    public void ResolveCutAt_ReadsTheDeprecatedDateAsNoonInTheOrganizationsZone()
    {
        // Arrange — noon in Chicago in September is 17:00 UTC
        var request = new CutVersionRequest { CutDate = new LocalDate(2026, 9, 18) };

        // Act
        var resolved = request.ResolveCutAt(Chicago);

        // Assert
        resolved.Should().Be(Instant.FromUtc(2026, 9, 18, 17, 0));
        request.UsesLegacyDates().Should().BeTrue();
    }

    [Fact]
    public void Validate_RequiresOneOfThem()
    {
        // Act
        var result = _validator.TestValidate(new CutVersionRequest());

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorMessage == "CutAt is required.");
    }

    [Fact]
    public void Validate_RefusesBoth()
    {
        // Arrange — which one to believe is a guess, so neither is taken
        var request = new CutVersionRequest { CutAt = Instant.FromUtc(2026, 9, 18, 2, 30), CutDate = new LocalDate(2026, 9, 18) };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.Errors.Should().ContainSingle(e => e.ErrorMessage == "Send CutAt or the deprecated CutDate, not both.");
    }
}
#pragma warning restore CS0618