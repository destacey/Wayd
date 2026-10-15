using FluentValidation.TestHelper;
using Wayd.Web.Api.Models.Ppm.ProjectTasks;

namespace Wayd.Web.Api.Tests.Sut.Models.Ppm.ProjectTasks;

public sealed class UpdateProjectTaskRequestValidatorTests
{
    private readonly UpdateProjectTaskRequestValidator _validator = new();

    private static UpdateProjectTaskRequest Request(decimal? estimatedEffortHours) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Draft the plan",
        ParentId = Guid.NewGuid(),
        EstimatedEffortHours = estimatedEffortHours,
    };

    [Fact]
    public void NoEstimatedEffortHours_Passes()
    {
        // Arrange
        var request = Request(estimatedEffortHours: null);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(r => r.EstimatedEffortHours);
    }

    [Fact]
    public void EstimatedEffortHours_Zero_Fails()
    {
        // Arrange
        var request = Request(estimatedEffortHours: 0);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.EstimatedEffortHours);
    }
}
