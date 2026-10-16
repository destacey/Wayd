using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using NodaTime;

namespace Wayd.Infrastructure.OpenApi;

/// <summary>
/// Describes the values that travel as one JSON string or number the way NSwag does: NodaTime dates and
/// instants as formatted strings, <see cref="TimeSpan"/> as a pattern-checked string, numbers as numbers, and
/// a flags enum by its names.
/// </summary>
/// <remarks>
/// <para>
/// The generator reads NodaTime types through their serializers' converters, which it cannot see into, so it
/// would otherwise describe <see cref="Instant"/> and <see cref="LocalDate"/> as objects. The client's
/// <c>Interface.liquid</c> template relies on <c>format: date</c> to type a calendar date as a string rather
/// than a <c>Date</c>.
/// </para>
/// <para>
/// The API's serializer accepts numbers written as strings, which the generator publishes as
/// <c>anyOf: [integer, string]</c> with a numeric <c>pattern</c>. Clients send numbers, NSwag never published
/// the alternative, and its TypeScript generator would type every number as <c>number | string</c>.
/// </para>
/// </remarks>
public sealed class ScalarSchemaTransformer : IOpenApiSchemaTransformer
{
    /// <summary>The string formats of the types sent as formatted strings.</summary>
    private static readonly Dictionary<Type, string> _stringFormats = new()
    {
        [typeof(Instant)] = "date-time",
        [typeof(LocalDate)] = "date",
        [typeof(LocalDateTime)] = "date-time",
        [typeof(LocalTime)] = "time",
        [typeof(OffsetDateTime)] = "date-time",
    };

    /// <summary>The JSON Schema type and format of each numeric type.</summary>
    private static readonly Dictionary<Type, (JsonSchemaType Type, string Format)> _numbers = new()
    {
        [typeof(byte)] = (JsonSchemaType.Integer, "int32"),
        [typeof(short)] = (JsonSchemaType.Integer, "int32"),
        [typeof(int)] = (JsonSchemaType.Integer, "int32"),
        [typeof(long)] = (JsonSchemaType.Integer, "int64"),
        [typeof(float)] = (JsonSchemaType.Number, "float"),
        [typeof(double)] = (JsonSchemaType.Number, "double"),
        [typeof(decimal)] = (JsonSchemaType.Number, "decimal"),
    };

    /// <summary>How <see cref="TimeSpan"/> is written: <c>[d.]hh:mm[:ss[.fffffff]]</c>.</summary>
    private const string TimeSpanPattern = @"^([0-9]{1}|(?:0[0-9]|1[0-9]|2[0-3])+):([0-5]?[0-9])(?::([0-5]?[0-9])(?:.(\d{1,9}))?)?$";

    /// <summary>Whether a type is one this describes as a string, which is therefore never a component.</summary>
    public static bool Describes(Type type) =>
        _stringFormats.ContainsKey(Nullable.GetUnderlyingType(type) ?? type) || (Nullable.GetUnderlyingType(type) ?? type) == typeof(TimeSpan);

    /// <inheritdoc />
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        Apply(schema, context.JsonTypeInfo.Type);

        // The generator gives the items of a list of dates no schema at all, and runs no transformer on them.
        if (context.JsonTypeInfo is { Kind: JsonTypeInfoKind.Enumerable, ElementType: { } elementType } && Describes(elementType) && schema.Items is null or OpenApiSchema)
        {
            var items = schema.Items as OpenApiSchema ?? new OpenApiSchema();
            Apply(items, elementType);
            schema.Items = items;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Describes a value of <paramref name="declared"/> type, if it is one of the types this covers. Also used
    /// for query and route parameters, whose schemas no schema transformer sees.
    /// </summary>
    public static void Apply(OpenApiSchema schema, Type declared)
    {
        var type = Nullable.GetUnderlyingType(declared) ?? declared;
        var nullable = declared != type || schema.Type?.HasFlag(JsonSchemaType.Null) == true;

        if (_stringFormats.TryGetValue(type, out var format))
        {
            Describe(schema, JsonSchemaType.String, format, nullable);
        }
        else if (type == typeof(TimeSpan))
        {
            // NSwag's mapper published every TimeSpan as nullable, required or not.
            Describe(schema, JsonSchemaType.String, null, nullable: true);
            schema.Pattern = TimeSpanPattern;
            schema.Examples = [JsonValue.Create("02:00:00")];
        }
        else if (_numbers.TryGetValue(type, out var number))
        {
            schema.Type = nullable ? number.Type | JsonSchemaType.Null : number.Type;
            schema.Format = number.Format;
            schema.Pattern = null;
            schema.AnyOf = null;
        }
        else if (type.IsEnum && type.IsDefined(typeof(FlagsAttribute), false))
        {
            // The string enum converter writes a combination as "A, B", so the generator lists no values for
            // a flags enum. NSwag lists the names and marks the enum x-enumFlags.
            schema.Type = nullable ? JsonSchemaType.String | JsonSchemaType.Null : JsonSchemaType.String;
            schema.Enum = [.. Enum.GetNames(type).Select(n => (JsonNode)JsonValue.Create(n))];
            schema.Extensions ??= new Dictionary<string, IOpenApiExtension>();
            schema.Extensions["x-enumFlags"] = new JsonNodeExtension(JsonValue.Create(true));
        }
    }

    /// <summary>Replaces whatever the generator inferred with a plain string, keeping its validation keywords.</summary>
    private static void Describe(OpenApiSchema schema, JsonSchemaType type, string? format, bool nullable)
    {
        schema.Type = nullable ? type | JsonSchemaType.Null : type;
        schema.Format = format;
        schema.Properties = null;
        schema.Required = null;
        schema.AdditionalProperties = null;
        schema.AnyOf = null;
        schema.OneOf = null;
        schema.AllOf = null;
        schema.Pattern = null;
        // Length rules a validator states on a date (NotEmpty, say) are not lengths of the string.
        schema.MinLength = null;
        schema.MaxLength = null;
    }
}
