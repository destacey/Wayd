using FluentValidation.TestHelper;
using NodaTime;
using Wayd.Web.Api.Models.Planning.Roadmaps;

namespace Wayd.Web.Api.Tests.Sut.Models.Planning.Roadmaps;

public sealed class UpdateRoadmapMilestoneRequestValidatorTests
{
    private readonly UpdateRoadmapMilestoneRequestValidator _validator = new();

    private static UpdateRoadmapMilestoneRequest Request(LocalDate date) => new()
    {
        RoadmapId = Guid.NewGuid(),
        ItemId = Guid.NewGuid(),
        Name = "General availability",
        Date = date,
    };

    [Fact]
    public void Date_Given_Passes()
    {
        // Arrange
        var request = Request(new LocalDate(2026, 5, 1));

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(r => r.Date);
    }

    [Fact]
    public void Date_LeftOut_Fails()
    {
        // Arrange — a request without a date binds the default, 1 January of year 1
        var request = Request(default);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Date);
    }
}
