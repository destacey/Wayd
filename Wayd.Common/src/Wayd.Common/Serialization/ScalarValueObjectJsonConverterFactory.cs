using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wayd.Common.Models;

namespace Wayd.Common.Serialization;

/// <summary>
/// A JSON converter factory for types that inherit from <see cref="ScalarValueObject{T}"/>.
/// Serializes the value object directly as its underlying primitive scalar (e.g. "CORE" or 42.5),
/// avoiding internal OOP implementation leakage ({"value": "CORE"}).
/// Supports backward-compatible deserialization from legacy {"value": ...} object payloads.
/// </summary>
public sealed class ScalarValueObjectJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return IsScalarValueObject(typeToConvert, out _, out _);
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        if (!IsScalarValueObject(typeToConvert, out _, out var valueType))
        {
            throw new InvalidOperationException($"Type '{typeToConvert.FullName}' does not inherit from ScalarValueObject<T>.");
        }

        var converterType = typeof(ScalarValueObjectConverter<,>).MakeGenericType(typeToConvert, valueType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    internal static bool IsScalarValueObject(Type type, [NotNullWhen(true)] out Type? scalarBaseType, [NotNullWhen(true)] out Type? valueType)
    {
        var current = type;
        while (current != null && current != typeof(object))
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(ScalarValueObject<>))
            {
                scalarBaseType = current;
                valueType = current.GetGenericArguments()[0];
                return true;
            }
            current = current.BaseType;
        }

        scalarBaseType = null;
        valueType = null;
        return false;
    }
}

internal sealed class ScalarValueObjectConverter<TDerived, TValue> : JsonConverter<TDerived>
    where TDerived : class
    where TValue : IComparable
{
    private static readonly Func<TValue, TDerived> Factory = CreateFactory();

    private static Func<TValue, TDerived> CreateFactory()
    {
        var ctor = typeof(TDerived).GetConstructor(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            [typeof(TValue)],
            null);

        if (ctor is null)
        {
            throw new InvalidOperationException(
                $"Type '{typeof(TDerived).FullName}' must define a constructor accepting a single parameter of type '{typeof(TValue).FullName}'.");
        }

        var param = Expression.Parameter(typeof(TValue), "value");
        var newExpr = Expression.New(ctor, param);
        return Expression.Lambda<Func<TValue, TDerived>>(newExpr, param).Compile();
    }

    public override TDerived? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        // Backward compatibility: handle legacy {"value": ...} object representation
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (string.Equals(prop.Name, "value", StringComparison.OrdinalIgnoreCase))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        return null;
                    }

                    var rawText = prop.Value.GetRawText();
                    var innerValue = JsonSerializer.Deserialize<TValue>(rawText, options);
                    return innerValue is not null ? Factory(innerValue) : null;
                }
            }

            throw new JsonException($"Expected property 'value' when deserializing {typeof(TDerived).Name} from an object.");
        }

        // Standard scalar deserialization
        var scalar = JsonSerializer.Deserialize<TValue>(ref reader, options);
        return scalar is not null ? Factory(scalar) : null;
    }

    public override void Write(Utf8JsonWriter writer, TDerived value, JsonSerializerOptions options)
    {
        if (value is ScalarValueObject<TValue> scalarVo)
        {
            JsonSerializer.Serialize(writer, scalarVo.Value, options);
        }
        else if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            throw new JsonException($"Expected value of type {typeof(ScalarValueObject<TValue>).FullName} but received {value.GetType().FullName}.");
        }
    }
}
