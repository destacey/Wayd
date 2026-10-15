using FluentValidation.TestHelper;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.Web.Api.Models.Ppm.ProjectTasks;

namespace Wayd.Web.Api.Tests.Sut.Models.Ppm.ProjectTasks;

public sealed class CreateProjectTaskRequestValidatorTests
{
    private readonly CreateProjectTaskRequestValidator _validator = new();

    private static CreateProjectTaskRequest Request(decimal? estimatedEffortHours) => new()
    {
        Name = "Draft the plan",
        TypeId = (int)ProjectTaskType.Task,
        Progress = 0,
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
