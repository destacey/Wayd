using FluentAssertions;
using Wayd.Common.Application.Scoring.ScoringModels.Commands;
using Wayd.Common.Domain.Scoring;

namespace Wayd.Common.Application.Tests.Sut.Scoring.ScoringModels.Commands;

public class AddScoringModelOutputCommandValidatorTests
{
    private readonly AddScoringModelOutputCommandValidator _validator = new();

    private static AddScoringModelOutputCommand CommandWithFormula(string formula) =>
        new(Guid.NewGuid(), "Score", "SCORE", formula, IsPrimary: true);

    [Fact]
    public void Validate_ShouldFail_WhenFormulaNestsBeyondMaxDepth()
    {
        // Arrange
        // Rejected here so the caller sees a field error, rather than reaching the domain and surfacing as a
        // generic command failure. The evaluator enforces the same bound regardless.
        var command = CommandWithFormula(new string('(', 200) + "BV" + new string(')', 200));

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(command.Formula))
            .Which.ErrorMessage.Should().Contain(ScoringFormulaEvaluator.MaxNestingDepth.ToString());
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenNestingIsWithinMaxDepth()
    {
        // Arrange
        var depth = ScoringFormulaEvaluator.MaxNestingDepth;
        var command = CommandWithFormula(new string('(', depth) + "BV" + new string(')', depth));

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldReportOnlyEmptiness_WhenFormulaIsEmpty()
    {
        // Arrange
        // The depth rule must not also fire on an empty formula, or the caller gets two errors for one mistake.
        var command = CommandWithFormula(string.Empty);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(command.Formula));
    }
}
