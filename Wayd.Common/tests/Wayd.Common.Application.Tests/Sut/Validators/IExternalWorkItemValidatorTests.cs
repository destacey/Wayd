using FluentValidation.TestHelper;
using NodaTime;
using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Validators;

namespace Wayd.Common.Application.Tests.Sut.Validators;

public class IExternalWorkItemValidatorTests
{
    private static readonly Instant _now = Instant.FromUtc(2026, 10, 4, 12, 0);

    private readonly IExternalWorkItemValidator _sut = new();

    [Theory]
    [InlineData(null, null, null)]
    [InlineData(0d, 0d, 0d)]
    [InlineData(3d, 8d, 5d)]
    public void Validate_WithNullOrNonNegativeEstimates_HasNoEstimateErrors(double? storyPoints, double? effort, double? size)
    {
        // Arrange
        var workItem = BuildWorkItem(storyPoints, effort, size);

        // Act
        var result = _sut.TestValidate(workItem);

        // Assert
        result.ShouldNotHaveValidationErrorFor(w => w.StoryPoints);
        result.ShouldNotHaveValidationErrorFor(w => w.Effort);
        result.ShouldNotHaveValidationErrorFor(w => w.Size);
    }

    [Fact]
    public void Validate_WithNegativeEstimates_HasAnErrorForEach()
    {
        // Arrange
        var workItem = BuildWorkItem(storyPoints: -1, effort: -2, size: -3);

        // Act
        var result = _sut.TestValidate(workItem);

        // Assert
        result.ShouldHaveValidationErrorFor(w => w.StoryPoints);
        result.ShouldHaveValidationErrorFor(w => w.Effort);
        result.ShouldHaveValidationErrorFor(w => w.Size);
    }

    private static IExternalWorkItem BuildWorkItem(double? storyPoints, double? effort, double? size)
    {
        var workItem = new Mock<IExternalWorkItem>();
        workItem.SetupGet(w => w.Id).Returns(101);
        workItem.SetupGet(w => w.Title).Returns("Sample work item");
        workItem.SetupGet(w => w.WorkType).Returns("User Story");
        workItem.SetupGet(w => w.WorkStatus).Returns("New");
        workItem.SetupGet(w => w.Created).Returns(_now);
        workItem.SetupGet(w => w.LastModified).Returns(_now);
        workItem.SetupGet(w => w.Tags).Returns([]);
        workItem.SetupGet(w => w.StoryPoints).Returns(storyPoints);
        workItem.SetupGet(w => w.Effort).Returns(effort);
        workItem.SetupGet(w => w.Size).Returns(size);
        return workItem.Object;
    }
}
