using FluentAssertions;
using Wayd.Common.Domain.Scoring;

namespace Wayd.Common.Domain.Tests.Sut.Scoring;

public class ScoringFormulaEvaluatorTests
{
    private static readonly string[] DefaultTokens = ["BV", "TC", "RR", "JS"];

    #region Validate

    [Fact]
    public void Validate_ShouldSucceed_ForWellFormedFormulaOverAllowedTokens()
    {
        // Arrange
        var formula = "(BV + TC + RR) / JS";

        // Act
        var result = ScoringFormulaEvaluator.Validate(formula, DefaultTokens);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldFail_WhenFormulaIsEmpty(string formula)
    {
        // Arrange
        // (formula supplied via InlineData)

        // Act
        var result = ScoringFormulaEvaluator.Validate(formula, DefaultTokens);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public void Validate_ShouldFail_WhenFormulaExceedsMaxLength()
    {
        // Arrange
        var formula = "BV" + new string('+', ScoringFormulaEvaluator.MaxFormulaLength);

        // Act
        var result = ScoringFormulaEvaluator.Validate(formula, DefaultTokens);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(ScoringFormulaEvaluator.MaxFormulaLength.ToString());
    }

    [Fact]
    public void Validate_ShouldFail_WhenFormulaIsNotParseable()
    {
        // Arrange
        var formula = "BV + + ";

        // Act
        var result = ScoringFormulaEvaluator.Validate(formula, DefaultTokens);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not a valid expression");
    }

    [Fact]
    public void Validate_ShouldFail_WhenFormulaCallsAFunction()
    {
        // Arrange
        var formula = "Max(BV, TC)";

        // Act
        var result = ScoringFormulaEvaluator.Validate(formula, DefaultTokens);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("must not call functions");
        result.Error.Should().Contain("Max");
    }

    [Fact]
    public void Validate_ShouldFail_WhenFormulaReferencesUnknownToken()
    {
        // Arrange
        var formula = "BV + Unknown";

        // Act
        var result = ScoringFormulaEvaluator.Validate(formula, DefaultTokens);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("unknown token");
        result.Error.Should().Contain("Unknown");
    }

    [Fact]
    public void Validate_ShouldBeCaseSensitive_ForTokenNames()
    {
        // Arrange
        var formula = "bv + TC";

        // Act
        var result = ScoringFormulaEvaluator.Validate(formula, DefaultTokens);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("bv");
    }

    #endregion

    #region GetReferencedTokens

    [Fact]
    public void GetReferencedTokens_ShouldReturnDistinctReferencedTokens()
    {
        // Arrange
        var formula = "BV + TC + BV";

        // Act
        var result = ScoringFormulaEvaluator.GetReferencedTokens(formula);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo("BV", "TC");
    }

    [Fact]
    public void GetReferencedTokens_ShouldFail_WhenFormulaIsEmpty()
    {
        // Arrange
        var formula = "  ";

        // Act
        var result = ScoringFormulaEvaluator.GetReferencedTokens(formula);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public void GetReferencedTokens_ShouldFail_WhenFormulaIsNotParseable()
    {
        // Arrange
        var formula = "BV +";

        // Act
        var result = ScoringFormulaEvaluator.GetReferencedTokens(formula);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not a valid expression");
    }

    #endregion

    #region Evaluate

    [Fact]
    public void Evaluate_ShouldComputeArithmeticResult()
    {
        // Arrange
        var formula = "(BV + TC + RR) / JS";
        var values = new Dictionary<string, decimal>
        {
            ["BV"] = 8m,
            ["TC"] = 5m,
            ["RR"] = 1m,
            ["JS"] = 2m,
        };

        // Act
        var result = ScoringFormulaEvaluator.Evaluate(formula, values);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(7m);
    }

    [Fact]
    public void Evaluate_ShouldPreserveDecimalPrecision()
    {
        // Arrange
        var formula = "BV / TC";
        var values = new Dictionary<string, decimal>
        {
            ["BV"] = 1m,
            ["TC"] = 8m,
        };

        // Act
        var result = ScoringFormulaEvaluator.Evaluate(formula, values);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0.125m);
    }

    [Fact]
    public void Evaluate_ShouldFail_OnDivideByZero()
    {
        // Arrange
        var formula = "BV / JS";
        var values = new Dictionary<string, decimal>
        {
            ["BV"] = 8m,
            ["JS"] = 0m,
        };

        // Act
        var result = ScoringFormulaEvaluator.Evaluate(formula, values);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("divided by zero");
    }

    [Fact]
    public void Evaluate_ShouldFail_WhenFormulaCallsAFunction()
    {
        // Arrange
        var formula = "Max(BV, TC)";
        var values = new Dictionary<string, decimal>
        {
            ["BV"] = 8m,
            ["TC"] = 5m,
        };

        // Act
        var result = ScoringFormulaEvaluator.Evaluate(formula, values);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("could not be evaluated");
    }

    [Fact]
    public void Evaluate_ShouldFail_WhenFormulaIsEmpty()
    {
        // Arrange
        var formula = string.Empty;

        // Act
        var result = ScoringFormulaEvaluator.Evaluate(formula, new Dictionary<string, decimal>());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    #endregion
}
