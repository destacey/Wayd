using System.Globalization;
using CSharpFunctionalExtensions;
using NCalc;
using NCalc.Exceptions;

namespace Wayd.Common.Domain.Scoring;

/// <summary>
/// Validates and evaluates scoring formula expressions over named criterion/output tokens.
/// </summary>
/// <remarks>
/// Backed by NCalc, restricted to pure arithmetic over named parameters (decimal math). No function
/// calls, reflection, or code execution are permitted — any function reference in a formula is rejected,
/// so this is safe for admin-authored (semi-trusted) input. Formulas are validated at definition time and
/// evaluated at scoring time; both surface failures as <see cref="Result"/> rather than throwing.
/// </remarks>
public static class ScoringFormulaEvaluator
{
    /// <summary>
    /// Upper bound on formula length, as a guard against pathological parse input.
    /// </summary>
    public const int MaxFormulaLength = 1000;

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

    private static Expression CreateExpression(string formula)
    {
        var expression = new Expression(formula, ExpressionOptions.DecimalAsDefault, CultureInfo.InvariantCulture);

        // Reject any function reference — formulas are pure arithmetic over named tokens only.
        expression.EvaluateFunction += (name, _) =>
            throw new NCalcEvaluationException($"Function '{name}' is not allowed in scoring formulas.");

        return expression;
    }
}
