using FluentAssertions;
using FluentValidation.TestHelper;
using NodaTime;
using Wayd.Web.Api.Models.Kpis;

namespace Wayd.Web.Api.Tests.Sut.Models.Kpis;

public sealed class ImportKpiMeasurementRequestValidatorTests
{
    private readonly ImportKpiMeasurementRequestValidator _validator = new();

    private static ImportKpiMeasurementRequest Request() => new() { KpiImportId = "kpi-1", Value = 12 };

    [Fact]
    public void APeriod_Passes()
    {
        // Arrange
        var request = Request();
        request.PeriodStart = new DateOnly(2026, 1, 1);
        request.PeriodEnd = new DateOnly(2026, 1, 31);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void NoTimeAtAll_Fails()
    {
        // Arrange
        var request = Request();

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void BothAPeriodAndAnAsOfDay_Fails()
    {
        // Arrange
        var request = Request();
        request.PeriodStart = new DateOnly(2026, 1, 1);
        request.PeriodEnd = new DateOnly(2026, 1, 31);
        request.AsOf = new DateOnly(2026, 1, 31);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void HalfAPeriod_Fails()
    {
        // Arrange
        var request = Request();
        request.PeriodStart = new DateOnly(2026, 1, 1);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.PeriodEnd);
    }

    [Theory]
    [InlineData("2026-03-06T21:00:00Z", 21)]
    [InlineData("2026-03-06T15:00:00-06:00", 21)]
    [InlineData("2026-03-06T15:00-06:00", 21)]
    public void AMomentWithAnOffset_ReadsAsThatInstant(string measuredAt, int utcHour)
    {
        // Arrange
        var request = Request();
        request.MeasuredAt = measuredAt;

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
        request.ToImportKpiMeasurementDto().MeasuredAt.Should().Be(Instant.FromUtc(2026, 3, 6, utcHour, 0));
    }

    [Theory]
    [InlineData("2026-03-06T15:00:00")]
    [InlineData("2026-03-06")]
    [InlineData("3/6/2026 3:00 PM")]
    public void AMomentWithoutAnOffset_Fails(string measuredAt)
    {
        // Arrange
        var request = Request();
        request.MeasuredAt = measuredAt;

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.MeasuredAt);
    }
}
