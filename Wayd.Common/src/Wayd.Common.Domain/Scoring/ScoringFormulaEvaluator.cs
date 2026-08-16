using System.Globalization;
using CSharpFunctionalExtensions;
using NCalc;
using NCalc.Exceptions;
using NCalc.Helpers;

namespace Wayd.Common.Domain.Scoring;

/// <summary>
/// Validates and evaluates scoring formula expressions over named criterion/output tokens.
/// </summary>
/// <remarks>
/// Backed by NCalc, restricted to pure arithmetic over named parameters (decimal math). No function
/// calls, reflection, or code execution are permitted — any function reference in a formula is rejected,
/// so this is safe for admin-authored (semi-trusted) input. Formulas are validated at definition time and
/// evaluated at scoring time; both surface failures as <see cref="Result"/> rather than throwing.
/// <para>
/// Every entry point re-applies both the <see cref="MaxFormulaLength"/> and <see cref="MaxNestingDepth"/>
/// bounds rather than trusting that validation ran earlier. Both guard recursion that would defeat the
/// <see cref="Result"/> contract entirely — an overflowed stack kills the process instead of returning a
/// failure — so they are checked wherever a formula string enters, including the paths that only ever see
/// already-persisted formulas.
/// </para>
/// </remarks>
public static class ScoringFormulaEvaluator
{
    /// <summary>
    /// Upper bound on formula length, as a guard against pathological parse input.
    /// </summary>
    /// <remarks>
    /// Length bounds recursion that <see cref="MaxNestingDepth"/> does not. A flat formula (<c>BV + BV + ...</c>)
    /// nests only one level deep, but parses into a left-deep tree that the evaluation visitor walks recursively,
    /// so around 5,000 terms overflows the stack during <see cref="Evaluate"/>. That needs roughly 25,000
    /// characters — far beyond this bound — so enforcing length on every entry point keeps it unreachable.
    /// </remarks>
    public const int MaxFormulaLength = 1000;

    /// <summary>
    /// Upper bound on parenthesis nesting depth.
    /// </summary>
    /// <remarks>
    /// NCalc parses with Parlot, a recursive-descent parser that recurses once per nesting level. Past roughly
    /// 140 levels — well inside <see cref="MaxFormulaLength"/> — the parse overflows the stack, and a
    /// <see cref="StackOverflowException"/> terminates the process rather than being catchable, so no
    /// <c>try</c>/<c>catch</c> below can contain it. Depth must therefore be rejected *before* the string reaches
    /// NCalc, by the counting scan in <see cref="ExceedsMaxNestingDepth"/> (which cannot itself recurse).
    /// </remarks>
    public const int MaxNestingDepth = 32;

    /// <summary>
    /// Returns whether <paramref name="formula"/> nests parentheses within <see cref="MaxNestingDepth"/>.
    /// </summary>
    /// <remarks>
    /// Exposed so request and command validators can reject over-deep formulas alongside their other field
    /// rules. This is a convenience for error reporting, not the enforcement point — the evaluator applies the
    /// same bound itself, because a formula can also reach it from a path no validator guards.
    /// </remarks>
    public static bool IsWithinMaxNestingDepth(string? formula) =>
        string.IsNullOrEmpty(formula) || !ExceedsMaxNestingDepth(formula);

    /// <summary>
    /// Validates that <paramref name="formula"/> is a well-formed arithmetic expression referencing only
    /// tokens in <paramref name="allowedTokens"/>. Does not evaluate.
    /// </summary>
    public static Result Validate(string formula, IReadOnlyCollection<string> allowedTokens)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            return Result.Failure("Formula must not be empty.");
        }

        if (formula.Length > MaxFormulaLength)
        {
            return Result.Failure($"Formula must not exceed {MaxFormulaLength} characters.");
        }

        if (ExceedsMaxNestingDepth(formula))
        {
            return Result.Failure($"Formula must not nest parentheses more than {MaxNestingDepth} levels deep.");
        }

        Expression expression;
        try
        {
            expression = CreateExpression(formula);

            if (expression.HasErrors())
            {
                return Result.Failure($"Formula is not a valid expression: {expression.Error?.Message ?? "unknown parse error"}.");
            }
        }
        catch (NCalcException ex)
        {
            return Result.Failure($"Formula is not a valid expression: {ex.Message}.");
        }

        List<string> referenced;
        List<string> functions;
        try
        {
            referenced = expression.GetParameterNames();
            functions = expression.GetFunctionNames();
        }
        catch (NCalcException ex)
        {
            return Result.Failure($"Formula is not a valid expression: {ex.Message}.");
        }

        if (functions.Count > 0)
        {
            return Result.Failure($"Formula must not call functions: {string.Join(", ", functions.Distinct(StringComparer.Ordinal))}.");
        }

        var allowed = new HashSet<string>(allowedTokens, StringComparer.Ordinal);
        var unknown = referenced.Where(r => !allowed.Contains(r)).Distinct(StringComparer.Ordinal).ToList();
        if (unknown.Count > 0)
        {
            return Result.Failure($"Formula references unknown token(s): {string.Join(", ", unknown)}.");
        }

        return Result.Success();
    }

    /// <summary>
    /// Returns the distinct token names referenced by <paramref name="formula"/>, or a failure if it does not parse.
    /// </summary>
    public static Result<IReadOnlyCollection<string>> GetReferencedTokens(string formula)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            return Result.Failure<IReadOnlyCollection<string>>("Formula must not be empty.");
        }

        if (formula.Length > MaxFormulaLength)
        {
            return Result.Failure<IReadOnlyCollection<string>>(
                $"Formula must not exceed {MaxFormulaLength} characters.");
        }

        if (ExceedsMaxNestingDepth(formula))
        {
            return Result.Failure<IReadOnlyCollection<string>>(
                $"Formula must not nest parentheses more than {MaxNestingDepth} levels deep.");
        }

        try
        {
            var expression = CreateExpression(formula);
            if (expression.HasErrors())
            {
                return Result.Failure<IReadOnlyCollection<string>>(
                    $"Formula is not a valid expression: {expression.Error?.Message ?? "unknown parse error"}.");
            }

            var names = expression.GetParameterNames().Distinct(StringComparer.Ordinal).ToArray();
            return Result.Success<IReadOnlyCollection<string>>(names);
        }
        catch (NCalcException ex)
        {
            return Result.Failure<IReadOnlyCollection<string>>($"Formula is not a valid expression: {ex.Message}.");
        }
    }

    /// <summary>
    /// Evaluates <paramref name="formula"/> with the supplied token values, returning the resulting decimal.
    /// </summary>
    public static Result<decimal> Evaluate(string formula, IReadOnlyDictionary<string, decimal> tokenValues)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            return Result.Failure<decimal>("Formula must not be empty.");
        }

        // Re-checked here, not just on the write path: this evaluates formulas already persisted, including any
        // stored before these bounds existed.
        if (formula.Length > MaxFormulaLength)
        {
            return Result.Failure<decimal>($"Formula must not exceed {MaxFormulaLength} characters.");
        }

        if (ExceedsMaxNestingDepth(formula))
        {
            return Result.Failure<decimal>($"Formula must not nest parentheses more than {MaxNestingDepth} levels deep.");
        }

        try
        {
            var expression = CreateExpression(formula);

            if (expression.HasErrors())
            {
                return Result.Failure<decimal>(
                    $"Formula is not a valid expression: {expression.Error?.Message ?? "unknown parse error"}.");
            }

            foreach (var (token, value) in tokenValues)
            {
                expression.Parameters[token] = value;
            }

            var result = expression.Evaluate();

            return result is null
                ? Result.Failure<decimal>("Formula evaluated to no value.")
                : Result.Success(Convert.ToDecimal(result, CultureInfo.InvariantCulture));
        }
        catch (DivideByZeroException)
        {
            return Result.Failure<decimal>("Formula evaluation divided by zero.");
        }
        catch (NCalcException ex)
        {
            return Result.Failure<decimal>($"Formula could not be evaluated: {ex.Message}.");
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            return Result.Failure<decimal>($"Formula could not be evaluated: {ex.Message}.");
        }
    }

    /// <summary>
    /// Parsing and evaluation settings shared by every scoring formula: decimal math, with overflow raised as a
    /// catchable error rather than silently wrapping. Immutable, so a single instance is safe to share.
    /// </summary>
    private static readonly ExpressionConfiguration Configuration = new()
    {
        Parsing = new LogicalExpressionParserOptions
        {
            FloatingPointNumberType = FloatingPointNumberType.Decimal,
        },
        Evaluation = new ExpressionEvaluationOptions
        {
            Math = new MathOptions
            {
                FloatingPointNumberType = FloatingPointNumberType.Decimal,
                OverflowProtection = true,
            },
        },
    };

    /// <summary>
    /// Returns whether <paramref name="formula"/> nests parentheses deeper than <see cref="MaxNestingDepth"/>.
    /// </summary>
    /// <remarks>
    /// Deliberately a flat character scan: it must stay safe on input that would overflow a recursive parser.
    /// Unbalanced closing parentheses cannot push the depth below zero, so a trailing run of <c>)</c> can never
    /// mask a deeply nested prefix.
    /// </remarks>
    private static bool ExceedsMaxNestingDepth(string formula)
    {
        var depth = 0;

        foreach (var character in formula)
        {
            switch (character)
            {
                case '(':
                    if (++depth > MaxNestingDepth)
                    {
                        return true;
                    }

                    break;

                case ')':
                    if (depth > 0)
                    {
                        depth--;
                    }

                    break;
            }
        }

        return false;
    }

    private static Expression CreateExpression(string formula)
    {
        var expression = new Expression(formula, Configuration, new ExpressionContext(), CultureInfo.InvariantCulture);

        // Reject any function reference — formulas are pure arithmetic over named tokens only.
        expression.EvaluateFunction += (name, _) =>
            throw new NCalcEvaluationException($"Function '{name}' is not allowed in scoring formulas.");

        return expression;
    }
}
