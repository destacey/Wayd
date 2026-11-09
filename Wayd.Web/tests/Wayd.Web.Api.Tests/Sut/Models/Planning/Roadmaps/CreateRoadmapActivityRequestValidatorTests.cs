using FluentValidation.TestHelper;
using NodaTime;
using Wayd.Web.Api.Models.Planning.Roadmaps;

namespace Wayd.Web.Api.Tests.Sut.Models.Planning.Roadmaps;

public sealed class CreateRoadmapActivityRequestValidatorTests
{
    private readonly CreateRoadmapActivityRequestValidator _validator = new();

    private static CreateRoadmapActivityRequest Request(int? order) => new()
    {
        RoadmapId = Guid.NewGuid(),
        Name = "Beta programme",
        Start = new LocalDate(2026, 5, 1),
        End = new LocalDate(2026, 5, 29),
        Order = order,
    };

    [Fact]
    public void NoOrder_Passes()
    {
        // Arrange
        var request = Request(order: null);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Order_One_Passes()
    {
        // Arrange
        var request = Request(order: 1);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(r => r.Order);
    }

    [Fact]
    public void Order_Zero_Fails()
    {
        // Arrange
        var request = Request(order: 0);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Order);
    }
}
