using FluentAssertions;
using FluentValidation.TestHelper;
using NodaTime;
using Wayd.Web.Api.Models.ProductManagement.Versions;

namespace Wayd.Web.Api.Tests.Sut.Models.ProductManagement.Versions;

/// <summary>
/// A version CSV row, and what it becomes. Its cut and released moments are instants, so the row's work
/// is reading timestamps that carry their offset and refusing ones that do not.
/// </summary>
public sealed class ImportVersionRequestTests
{
    private readonly ImportVersionRequestValidator _validator = new();

    private static ImportVersionRequest Row(string? cutAt = null, string? releasedAt = null) =>
        new()
        {
            ProductId = Guid.CreateVersion7(),
            Number = "4.12.0",
            CutAt = cutAt,
            ReleasedAt = releasedAt,
        };

    [Fact]
    public void ToImportVersionDto_ReadsEachMomentAsTheInstantItsOffsetDescribes()
    {
        // Arrange — a late-evening US Central release, which is the next day in UTC
        var request = Row(cutAt: "2026-09-17T10:00:00Z", releasedAt: "2026-09-17T21:30:00-05:00");

        // Act
        var dto = request.ToImportVersionDto();

        // Assert
        dto.CutAt.Should().Be(Instant.FromUtc(2026, 9, 17, 10, 0));
        dto.ReleasedAt.Should().Be(Instant.FromUtc(2026, 9, 18, 2, 30));
    }

    [Fact]
    public void Validate_AcceptsARowWithNeitherMoment()
    {
        // Act
        var result = _validator.TestValidate(Row());

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("2026-09-18")]
    [InlineData("2026-09-18T02:30:00")]
    [InlineData("yesterday")]
    public void Validate_RefusesAMomentWithoutAnOffset(string value)
    {
        // Arrange — read in the server's zone, a bare date or local time would shift by whatever that is
        var request = Row(cutAt: value, releasedAt: value);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(v => v.CutAt);
        result.ShouldHaveValidationErrorFor(v => v.ReleasedAt);
    }

    [Fact]
    public void Validate_RefusesAReleaseBeforeTheCut()
    {
        // Arrange — the same day, but an hour earlier
        var request = Row(cutAt: "2026-09-17T15:00:00Z", releasedAt: "2026-09-17T14:00:00Z");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(v => v.ReleasedAt)
            .WithErrorMessage("A version cannot be released before it was cut.");
    }

    [Fact]
    public void Validate_ComparesMomentsWrittenInDifferentZones()
    {
        // Arrange — 21:30 in Chicago is after 01:00 UTC the same night
        var request = Row(cutAt: "2026-09-18T01:00:00Z", releasedAt: "2026-09-17T21:30:00-05:00");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
