using FluentAssertions;
using FluentValidation.TestHelper;
using NodaTime;
using Wayd.Web.Api.Models.ProductManagement.Versions;

namespace Wayd.Web.Api.Tests.Sut.Models.ProductManagement.Versions;

/// <summary>
/// The version date correction, which still accepts the deprecated CutDate and ReleasedDate.
/// </summary>
#pragma warning disable CS0618 // The deprecated fields are what these cover.
public sealed class CorrectVersionDatesRequestTests
{
    private static readonly DateTimeZone Chicago = DateTimeZoneProviders.Tzdb["America/Chicago"];

    private readonly CorrectVersionDatesRequestValidator _validator = new();

    [Fact]
    public void Resolve_MixesAnInstantWithADeprecatedDate()
    {
        // Arrange — a caller part way through moving over
        var cutAt = Instant.FromUtc(2026, 9, 17, 15, 0);
        var request = new CorrectVersionDatesRequest { CutAt = cutAt, ReleasedDate = new LocalDate(2026, 9, 18) };

        // Act & Assert
        request.ResolveCutAt(Chicago).Should().Be(cutAt);
        request.ResolveReleasedAt(Chicago).Should().Be(Instant.FromUtc(2026, 9, 18, 17, 0));
        request.UsesLegacyDates().Should().BeTrue();
    }

    [Fact]
    public void Resolve_LeavesAnOmittedValueEmpty_SoTheCorrectionClearsIt()
    {
        // Arrange
        var request = new CorrectVersionDatesRequest();

        // Act & Assert
        request.ResolveCutAt(Chicago).Should().BeNull();
        request.ResolveReleasedAt(Chicago).Should().BeNull();
        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_RefusesBothFormsOfOneValue()
    {
        // Arrange
        var request = new CorrectVersionDatesRequest
        {
            CutAt = Instant.FromUtc(2026, 9, 17, 15, 0),
            CutDate = new LocalDate(2026, 9, 17),
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.Errors.Should().ContainSingle(e => e.ErrorMessage == "Send CutAt or the deprecated CutDate, not both.");
    }
}
#pragma warning restore CS0618