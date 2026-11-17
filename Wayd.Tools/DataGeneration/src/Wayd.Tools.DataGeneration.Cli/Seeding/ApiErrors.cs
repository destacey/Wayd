using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Serialization;
using Wayd.Tools.DataGeneration.Cli.Client.Models;

namespace Wayd.Tools.DataGeneration.Cli.Seeding;

/// <summary>
/// Puts what the API said about a refused call into words for the operator.
/// </summary>
/// <remarks>
/// The generated client throws the problem details it read as the exception, but leaves its
/// <see cref="Exception.Message"/> generic, so the reason has to be read back out of the body's fields.
/// </remarks>
public static class ApiErrors
{
    /// <summary>The status, title, detail and each validation error the API answered with.</summary>
    public static string Describe(ApiException exception)
    {
        if (exception is not ProblemDetails problem)
            return $"{exception.ResponseStatusCode}: {exception.Message}";

        List<string> parts = [.. new[] { problem.Title, problem.Detail }.OfType<string>().Where(p => p.Length > 0)];

        if (problem is HttpValidationProblemDetails { Errors: { } errors })
        {
            parts.AddRange(errors.AdditionalData.Select(e => $"{e.Key}: {string.Join(" ", Messages(e.Value))}"));
        }

        return parts.Count == 0
            ? $"{exception.ResponseStatusCode}"
            : $"{exception.ResponseStatusCode} {string.Join(" ", parts)}";
    }

    /// <summary>The messages under one field: the generated model keeps them as untyped JSON.</summary>
    private static IEnumerable<string> Messages(object? value) => value switch
    {
        UntypedArray array => array.GetValue().SelectMany(Messages),
        UntypedString text => text.GetValue() is { } s ? [s] : [],
        IEnumerable<object> items => items.SelectMany(Messages),
        null => [],
        _ => [value.ToString() ?? string.Empty],
    };
}
