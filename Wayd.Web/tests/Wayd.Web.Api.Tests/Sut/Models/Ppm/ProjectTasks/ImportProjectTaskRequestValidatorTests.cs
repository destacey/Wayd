using FluentValidation.TestHelper;
using Wayd.Web.Api.Models.Ppm.ProjectTasks;

namespace Wayd.Web.Api.Tests.Sut.Models.Ppm.ProjectTasks;

public sealed class ImportProjectTaskRequestValidatorTests
{
    private readonly ImportProjectTaskRequestValidator _validator = new();

    private static ImportProjectTaskRequest Request(decimal? progress = null, decimal? estimatedEffortHours = null) => new()
    {
        ProjectKey = "APOLLO",
        Name = "Draft the plan",
        StageName = "Planning",
        Progress = progress,
        EstimatedEffortHours = estimatedEffortHours,
    };

    [Fact]
    public void BlankProgressAndEffort_Pass()
    {
        // Arrange
        var request = Request();

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(t => t.Progress);
        result.ShouldNotHaveValidationErrorFor(t => t.EstimatedEffortHours);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Progress_OutsideZeroToHundred_Fails(decimal progress)
    {
        // Arrange
        var request = Request(progress: progress);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(t => t.Progress);
    }

    [Fact]
    public void EstimatedEffortHours_Zero_Fails()
    {
        // Arrange
        var request = Request(estimatedEffortHours: 0);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(t => t.EstimatedEffortHours);
    }
}
