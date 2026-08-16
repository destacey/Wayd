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
    public void Validate_ShouldFail_WhenFormulaNestsBeyondMaxDepth()
    {
        // Arrange
        // Deep enough to overflow NCalc's recursive-descent parser, but well inside MaxFormulaLength — the
        // length bound alone does not stop it, and the resulting StackOverflowException would be uncatchable.
        const int depth = 200;
        var formula = new string('(', depth) + "BV" + new string(')', depth);

        // Act
        var result = ScoringFormulaEvaluator.Validate(formula, DefaultTokens);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(ScoringFormulaEvaluator.MaxNestingDepth.ToString());
        formula.Length.Should().BeLessThan(ScoringFormulaEvaluator.MaxFormulaLength);
    }

    [Fact]
    public void Validate_ShouldFail_WhenUnbalancedOpeningParenthesesNestBeyondMaxDepth()
    {
        // Arrange
        // The unbalanced form crashes the parser too, so the guard must not depend on well-formed input.
        var formula = new string('(', 200) + "BV";

        // Act
        var result = ScoringFormulaEvaluator.Validate(formula, DefaultTokens);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(ScoringFormulaEvaluator.MaxNestingDepth.ToString());
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenNestingIsWithinMaxDepth()
    {
        // Arrange
        var depth = ScoringFormulaEvaluator.MaxNestingDepth;
        var formula = new string('(', depth) + "BV" + new string(')', depth);

        // Act
        var result = ScoringFormulaEvaluator.Validate(formula, DefaultTokens);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_ForManySequentialGroupsWithinMaxDepth()
    {
        // Arrange
        // Sequential groups reopen at depth 1 repeatedly; only true nesting may count toward the bound.
        var formula = string.Join(" + ", Enumerable.Repeat("(BV)", 100));

        // Act
        var result = ScoringFormulaEvaluator.Validate(formula, DefaultTokens);

        // Assert
        result.IsSuccess.Should().BeTrue();
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

    #region IsWithinMaxNestingDepth

    [Fact]
    public void IsWithinMaxNestingDepth_ShouldBeFalse_WhenNestingExceedsMaxDepth()
    {
        // Arrange
        var formula = new string('(', 200) + "BV" + new string(')', 200);

        // Act
        var result = ScoringFormulaEvaluator.IsWithinMaxNestingDepth(formula);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsWithinMaxNestingDepth_ShouldBeTrue_AtExactlyMaxDepth()
    {
        // Arrange
        var depth = ScoringFormulaEvaluator.MaxNestingDepth;
        var formula = new string('(', depth) + "BV" + new string(')', depth);

        // Act
        var result = ScoringFormulaEvaluator.IsWithinMaxNestingDepth(formula);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsWithinMaxNestingDepth_ShouldBeTrue_WhenFormulaIsAbsent(string? formula)
    {
        // Arrange
        // Emptiness is NotEmpty()'s job in the validators; this rule must not also fail it and double-report.

        // Act
        var result = ScoringFormulaEvaluator.IsWithinMaxNestingDepth(formula);

        // Assert
        result.Should().BeTrue();
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
    public void GetReferencedTokens_ShouldFail_WhenFormulaExceedsMaxLength()
    {
        // Arrange
        var formula = "BV" + new string('+', ScoringFormulaEvaluator.MaxFormulaLength);

        // Act
        var result = ScoringFormulaEvaluator.GetReferencedTokens(formula);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(ScoringFormulaEvaluator.MaxFormulaLength.ToString());
    }

    [Fact]
    public void GetReferencedTokens_ShouldFail_WhenFormulaNestsBeyondMaxDepth()
    {
        // Arrange
        var formula = new string('(', 200) + "BV" + new string(')', 200);

        // Act
        var result = ScoringFormulaEvaluator.GetReferencedTokens(formula);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(ScoringFormulaEvaluator.MaxNestingDepth.ToString());
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
    public void Evaluate_ShouldFail_WhenFormulaExceedsMaxLength()
    {
        // Arrange
        // A flat chain nests only one level deep, so the depth bound does not apply — but it parses into a
        // left-deep tree that the evaluation visitor walks recursively, overflowing the stack at roughly 5,000
        // terms. Length is the bound that keeps that unreachable, and Evaluate sees already-persisted formulas.
        var formula = string.Join(" + ", Enumerable.Repeat("BV", 5_000));
        var values = new Dictionary<string, decimal> { ["BV"] = 2m };

        // Act
        var result = ScoringFormulaEvaluator.Evaluate(formula, values);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(ScoringFormulaEvaluator.MaxFormulaLength.ToString());
        ScoringFormulaEvaluator.IsWithinMaxNestingDepth(formula).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_ShouldSucceed_ForLongestFlatFormulaWithinMaxLength()
    {
        // Arrange
        // The most terms that fit inside MaxFormulaLength must still evaluate, confirming the length bound sits
        // safely below the recursion limit rather than merely near it.
        var terms = ScoringFormulaEvaluator.MaxFormulaLength / 5;
        var formula = string.Join(" + ", Enumerable.Repeat("BV", terms));
        var values = new Dictionary<string, decimal> { ["BV"] = 2m };

        // Act
        var result = ScoringFormulaEvaluator.Evaluate(formula, values);

        // Assert
        formula.Length.Should().BeLessThanOrEqualTo(ScoringFormulaEvaluator.MaxFormulaLength);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(terms * 2m);
    }

    [Fact]
    public void Evaluate_ShouldFail_WhenFormulaNestsBeyondMaxDepth()
    {
        // Arrange
        // Evaluate re-parses stored formulas, so it must reject depth independently of the write path — anything
        // persisted before the bound existed would otherwise still crash the process.
        var formula = new string('(', 200) + "BV" + new string(')', 200);
        var values = new Dictionary<string, decimal> { ["BV"] = 8m };

        // Act
        var result = ScoringFormulaEvaluator.Evaluate(formula, values);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(ScoringFormulaEvaluator.MaxNestingDepth.ToString());
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
