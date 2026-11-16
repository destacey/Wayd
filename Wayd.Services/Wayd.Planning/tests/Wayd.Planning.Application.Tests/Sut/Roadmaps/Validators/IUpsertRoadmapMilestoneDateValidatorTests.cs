using FluentValidation.TestHelper;
using NodaTime;
using Wayd.Planning.Application.Roadmaps.Validators;
using Wayd.Planning.Domain.Interfaces.Roadmaps;

namespace Wayd.Planning.Application.Tests.Sut.Roadmaps.Validators;

public sealed class IUpsertRoadmapMilestoneDateValidatorTests
{
    private readonly IUpsertRoadmapMilestoneDateValidator _validator = new();

    private sealed record MilestoneDate(LocalDate Date) : IUpsertRoadmapMilestoneDate;

    [Fact]
    public void Date_Given_Passes()
    {
        // Arrange
        var date = new MilestoneDate(new LocalDate(2026, 5, 1));

        // Act
        var result = _validator.TestValidate(date);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Date_Default_Fails()
    {
        // Arrange — 1 January of year 1, which a request left without a date carries
        var date = new MilestoneDate(default);

        // Act
        var result = _validator.TestValidate(date);

        // Assert
        result.ShouldHaveValidationErrorFor(d => d.Date);
    }
}
