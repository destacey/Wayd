using FluentValidation.TestHelper;
using Wayd.Web.Api.Models.Kpis;

namespace Wayd.Web.Api.Tests.Sut.Models.Kpis;

public sealed class ImportKpiCheckpointRequestValidatorTests
{
    private readonly ImportKpiCheckpointRequestValidator _validator = new();

    private static ImportKpiCheckpointRequest Row(string kpiImportId = "kpi-1", string label = "Q1") => new()
    {
        KpiImportId = kpiImportId,
        Date = new DateOnly(2026, 3, 31),
        Label = label,
        PlannedValue = 12,
    };

    [Fact]
    public void CompleteRow_Passes()
    {
        // Act
        var result = _validator.TestValidate(Row());

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void MissingKpiImportId_Fails()
    {
        // Act
        var result = _validator.TestValidate(Row(kpiImportId: ""));

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.KpiImportId);
    }

    [Fact]
    public void MissingDate_Fails()
    {
        // Arrange
        var row = Row();
        row.Date = default;

        // Act
        var result = _validator.TestValidate(row);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Date);
    }

    [Theory]
    [InlineData("")]
    [InlineData("A label well past the thirty-two character limit")]
    public void UnreadableLabel_Fails(string label)
    {
        // Act
        var result = _validator.TestValidate(Row(label: label));

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Label);
    }
}
