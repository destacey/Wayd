using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;
using Wayd.Common.Serialization;

namespace Wayd.Common.Models;

/// <summary>
/// Represents a strongly-typed domain value object wrapping a single primitive scalar value (e.g. string, decimal, int).
/// Provides automatic value equality, implicit cast to the scalar type, and seamless scalar JSON serialization
/// via <see cref="ScalarValueObjectJsonConverterFactory"/> so it serializes cleanly over message buses, outbox envelopes,
/// activity logs, and API payloads without leaking internal implementation details ({"value": "..."}).
/// </summary>
/// <typeparam name="T">The underlying primitive scalar type, which must implement <see cref="IComparable"/>.</typeparam>
[JsonConverter(typeof(ScalarValueObjectJsonConverterFactory))]
public abstract class ScalarValueObject<T> : ValueObject where T : IComparable
{
    protected ScalarValueObject()
    {
        Value = default!;
    }

    protected ScalarValueObject(T value)
    {
        Value = value;
    }

    /// <summary>
    /// The underlying scalar value.
    /// </summary>
    public T Value { get; protected init; }

    protected override IEnumerable<IComparable> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value?.ToString() ?? string.Empty;

    public static implicit operator T(ScalarValueObject<T> valueObject) => valueObject.Value;
}

