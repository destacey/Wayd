using NodaTime;
using Wayd.Common.Models;
using Wayd.Planning.Application.PlanningIntervals.Commands;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.PlanningIntervals.Commands;

public sealed class CreatePlanningIntervalCommandValidatorTests : IDisposable
{
    private static readonly LocalDateRange Dates = new(new LocalDate(2026, 1, 5), new LocalDate(2026, 3, 29));

    private readonly FakePlanningDbContext _dbContext = new();

    public void Dispose() => _dbContext.Dispose();

    private Task<FluentValidation.Results.ValidationResult> Validate(CreatePlanningIntervalCommand command) =>
        new CreatePlanningIntervalCommandValidator(_dbContext).ValidateAsync(command, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Validate_ShouldRefuseAName_ThatDiffersFromAnExistingOneOnlyByALeadingSpace()
    {
        // Arrange — the name is trimmed when stored, so " Q1" is "Q1" to the unique index
        _dbContext.AddPlanningInterval(new PlanningIntervalFaker().WithName("Q1").Generate());

        // Act
        var result = await Validate(new CreatePlanningIntervalCommand(" Q1", null, Dates, 2, null));

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorMessage == "The Planning Interval name already exists.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(53)]
    public async Task Validate_ShouldRefuseIterationWeeks_OutsideOneToFiftyTwo(int iterationWeeks)
    {
        // Act
        var result = await Validate(new CreatePlanningIntervalCommand("Q1", null, Dates, iterationWeeks, null));

        // Assert
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CreatePlanningIntervalCommand.IterationWeeks));
    }

    [Fact]
    public async Task Validate_ShouldRefuseAnIterationPrefix_TooLongForTheIterationName()
    {
        // Act
        var result = await Validate(new CreatePlanningIntervalCommand("Q1", null, Dates, 2, new string('I', 33)));

        // Assert
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CreatePlanningIntervalCommand.IterationPrefix));
    }
}
