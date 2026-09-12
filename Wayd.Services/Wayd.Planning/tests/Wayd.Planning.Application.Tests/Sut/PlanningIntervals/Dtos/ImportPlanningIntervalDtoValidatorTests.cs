using FluentValidation.TestHelper;
using NodaTime;
using Wayd.Planning.Application.PlanningIntervals.Dtos;

namespace Wayd.Planning.Application.Tests.Sut.PlanningIntervals.Dtos;

/// <summary>
/// The row rules that hold wherever a row came from, rather than only for the one endpoint that parses a
/// CSV into it.
/// </summary>
public sealed class ImportPlanningIntervalDtoValidatorTests
{
    private static readonly LocalDate Start = new(2026, 1, 5);
    private static readonly LocalDate End = new(2026, 2, 15);

    private readonly ImportPlanningIntervalDtoValidator _validator = new();

    private static ImportPlanningIntervalDto Row(
        string name = "PI 2026.1",
        string? description = "The first interval of the year",
        LocalDate? start = null,
        LocalDate? end = null,
        int iterationWeeks = 2,
        string? iterationPrefix = "PI26.1-") =>
        new(name, description, start ?? Start, end ?? End, iterationWeeks, iterationPrefix, []);

    [Fact]
    public void Validator_RejectsARowWithNoName()
    {
        // Arrange & Act & Assert
        _validator.TestValidate(Row(name: "")).ShouldHaveValidationErrorFor(p => p.Name);
    }

    [Fact]
    public void Validator_RejectsANameOverTheColumnLength()
    {
        // Arrange & Act & Assert
        _validator.TestValidate(Row(name: new string('x', 129))).ShouldHaveValidationErrorFor(p => p.Name);
    }

    [Fact]
    public void Validator_RejectsADescriptionOverTheColumnLength()
    {
        // Arrange & Act & Assert
        _validator.TestValidate(Row(description: new string('x', 2049)))
            .ShouldHaveValidationErrorFor(p => p.Description);
    }

    [Fact]
    public void Validator_RejectsAnEndBeforeTheStart()
    {
        // Arrange & Act & Assert
        _validator.TestValidate(Row(start: End, end: Start)).ShouldHaveValidationErrorFor(p => p.End);
    }

    [Fact]
    public void Validator_RejectsACadenceThatCannotGenerateIterations()
    {
        // Arrange & Act & Assert
        _validator.TestValidate(Row(iterationWeeks: 0)).ShouldHaveValidationErrorFor(p => p.IterationWeeks);
    }

    [Fact]
    public void Validator_AcceptsARowWithNoDescriptionOrPrefix()
    {
        // Arrange & Act & Assert — both are optional; iterations are then named by their number alone
        _validator.TestValidate(Row(description: null, iterationPrefix: null))
            .ShouldNotHaveAnyValidationErrors();
    }
}
