using FluentValidation.TestHelper;
using Wayd.Web.Api.Models.Ppm.ProjectTasks;

namespace Wayd.Web.Api.Tests.Sut.Models.Ppm.ProjectTasks;

public sealed class UpdateProjectTaskPlacementRequestValidatorTests
{
    private readonly UpdateProjectTaskPlacementRequestValidator _validator = new();

    [Fact]
    public void NoOrder_Passes()
    {
        // Arrange
        var request = new UpdateProjectTaskPlacementRequest { TaskId = Guid.NewGuid(), ParentId = Guid.NewGuid(), Order = null };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(r => r.Order);
    }

    [Fact]
    public void Order_Zero_Fails()
    {
        // Arrange
        var request = new UpdateProjectTaskPlacementRequest { TaskId = Guid.NewGuid(), ParentId = Guid.NewGuid(), Order = 0 };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Order)
            .WithErrorMessage("Order must be greater than 0 when specified.");
    }
}
