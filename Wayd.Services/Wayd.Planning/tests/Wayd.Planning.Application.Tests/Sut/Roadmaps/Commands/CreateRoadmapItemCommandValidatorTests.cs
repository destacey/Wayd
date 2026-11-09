using FluentValidation.TestHelper;
using NodaTime;
using OneOf;
using Wayd.Common.Models;
using Wayd.Planning.Application.Roadmaps.Commands;
using Wayd.Planning.Application.Roadmaps.Validators;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Interfaces.Roadmaps;

namespace Wayd.Planning.Application.Tests.Sut.Roadmaps.Commands;

public sealed class CreateRoadmapItemCommandValidatorTests
{
    private readonly CreateRoadmapItemCommandValidator _validator = new(
        new IUpsertRoadmapActivityValidator(),
        new IUpsertRoadmapMilestoneValidator(),
        new IUpsertRoadmapTimeboxValidator());

    private static CreateRoadmapItemCommand Command(int? order) =>
        new(Guid.NewGuid(),
            OneOf<IUpsertRoadmapActivity, IUpsertRoadmapMilestone, IUpsertRoadmapTimebox>.FromT0(
                new TestUpsertRoadmapActivity(new LocalDateRange(new LocalDate(2026, 5, 1), new LocalDate(2026, 5, 29)))),
            order);

    [Fact]
    public void NoOrder_Passes()
    {
        // Arrange
        var command = Command(order: null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Order_One_Passes()
    {
        // Arrange
        var command = Command(order: 1);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(c => c.Order);
    }

    [Fact]
    public void Order_Zero_Fails()
    {
        // Arrange
        var command = Command(order: 0);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Order);
    }
}
