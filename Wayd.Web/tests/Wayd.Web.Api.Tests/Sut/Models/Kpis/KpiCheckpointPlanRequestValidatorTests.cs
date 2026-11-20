using FluentValidation.TestHelper;
using NodaTime;
using Wayd.Web.Api.Models.Kpis;

namespace Wayd.Web.Api.Tests.Sut.Models.Kpis;

public sealed class KpiCheckpointPlanRequestValidatorTests
{
    private readonly KpiCheckpointPlanRequestValidator _validator = new();

    private static KpiCheckpointRequest Checkpoint(string label = "Q1") => new()
    {
        Date = new LocalDate(2026, 3, 31),
        Label = label,
        PlannedValue = 12,
    };

    [Fact]
    public void NullCheckpoint_Fails()
    {
        // Arrange
        var request = new KpiCheckpointPlanRequest { Checkpoints = [Checkpoint(), null!] };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor("Checkpoints[1]");
    }

    [Fact]
    public void BlankLabel_Fails()
    {
        // Arrange
        var request = new KpiCheckpointPlanRequest { Checkpoints = [Checkpoint(" ")] };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor("Checkpoints[0].Label");
    }

    [Fact]
    public void LabelTooLong_Fails()
    {
        // Arrange
        var request = new KpiCheckpointPlanRequest { Checkpoints = [Checkpoint(new string('x', 33))] };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor("Checkpoints[0].Label");
    }

    [Fact]
    public void EmptyPlan_Passes()
    {
        // Arrange
        var request = new KpiCheckpointPlanRequest();

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
