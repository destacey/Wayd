using FluentAssertions;
using FluentValidation.TestHelper;
using Wayd.Web.Api.Models.Kpis;

namespace Wayd.Web.Api.Tests.Sut.Models.Kpis;

public sealed class ImportKpiRequestValidatorTests
{
    private readonly ImportKpiRequestValidator _validator = new();

    private static ImportKpiRequest Request(string? atRiskKind = null, double? atRiskValue = null) => new()
    {
        ImportId = "kpi-1",
        SubjectId = Guid.NewGuid(),
        Name = "Throughput",
        Direction = "Increase",
        TargetValue = 20,
        AtRiskKind = atRiskKind,
        AtRiskValue = atRiskValue,
    };

    [Fact]
    public void AtRiskValueWithoutAKind_Fails()
    {
        // Arrange
        var request = Request(atRiskValue: 75);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.AtRiskKind);
    }

    [Fact]
    public void AtRiskKindWithoutAValue_Fails()
    {
        // Arrange
        var request = Request(atRiskKind: "Absolute");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.AtRiskValue);
    }

    [Fact]
    public void NoAtRiskThreshold_Passes()
    {
        // Arrange
        var request = Request();

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(r => r.AtRiskKind);
        result.ShouldNotHaveValidationErrorFor(r => r.AtRiskValue);
    }

    [Fact]
    public void ABaselineWindowWithAStartButNoEnd_Fails()
    {
        // Arrange
        var request = Request();
        request.BaselineValue = 10;
        request.BaselineWindowStart = new DateOnly(2025, 10, 1);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.BaselineWindowEnd);
    }

    [Fact]
    public void AnUnknownWindowKind_Fails()
    {
        // Arrange
        var request = Request();
        request.WindowKind = "Weekly";

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.WindowKind);
    }

    [Fact]
    public void ARollingWindow_ConvertsToTheDomainsWindow()
    {
        // Arrange
        var request = Request();
        request.WindowKind = "rolling";
        request.WindowRollingCount = 90;
        request.WindowRollingUnit = "Days";

        // Act
        var dto = request.ToImportKpiDto([], []);

        // Assert
        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
        dto.Window.Should().Be(Wayd.Common.Domain.Kpis.KpiMeasurementWindow.Rolling(90, Wayd.Common.Domain.Kpis.KpiRollingUnit.Days));
    }

    [Fact]
    public void ARollingAverageOutOfRange_Fails()
    {
        // Arrange
        var request = Request();
        request.RollingAverageOf = 1;

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.RollingAverageOf);
    }
}
