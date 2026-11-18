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
}
