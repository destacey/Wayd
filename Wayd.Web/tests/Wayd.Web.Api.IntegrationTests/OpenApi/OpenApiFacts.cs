using System.Text.Json;
using System.Text.Json.Nodes;

namespace Wayd.Web.Api.IntegrationTests.OpenApi;

/// <summary>
/// Reduces an OpenAPI 3.0 document to the facts a client generator or an agent acts on, each keyed by where
/// it is, so two documents written by different generators can be compared by meaning rather than by text.
/// </summary>
/// <remarks>
/// <para>
/// Operations are keyed by method and path, not by operation id, so a renamed operation shows up as one
/// changed <c>operationId</c> fact rather than as one operation missing and another added. Schemas are keyed
/// by component name.
/// </para>
/// <para>
/// A schema's properties are read through its <c>allOf</c> bases, so a generator that flattens inheritance and
/// one that keeps it agree on every property and differ only in the schema's <c>bases</c> fact. A reference
/// wrapped in a one-element <c>allOf</c>, <c>oneOf</c> or <c>anyOf</c> — how a generator attaches
/// <c>nullable</c> or a description to a <c>$ref</c> — reads as the reference itself.
/// </para>
/// <para>
/// Left out: response descriptions, which no consumer reads, and the order of anything a reader treats as a
/// set (required names, tags, media types).
/// </para>
/// </remarks>
public static class OpenApiFacts
{
    /// <summary>The value a fact takes on the side of the comparison where it does not exist.</summary>
    public const string Absent = "(absent)";

    private const string ComponentPrefix = "#/components/schemas/";

    /// <summary>The validation keywords FluentValidation rules and data annotations are published as.</summary>
    private static readonly string[] _validationKeywords =
    [
        "minLength", "maxLength", "minimum", "maximum", "exclusiveMinimum", "exclusiveMaximum", "pattern",
        "minItems", "maxItems", "uniqueItems", "multipleOf",
    ];

    private static readonly string[] _httpMethods = ["get", "put", "post", "delete", "options", "head", "patch", "trace"];

    /// <summary>Every fact the document states, keyed by its location.</summary>
    public static SortedDictionary<string, string> Extract(JsonObject document)
    {
        var facts = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var schemas = document["components"]?["schemas"]?.AsObject() ?? [];

        foreach (var (path, pathItem) in document["paths"]?.AsObject() ?? [])
        {
            foreach (var (method, node) in pathItem!.AsObject())
            {
                if (!_httpMethods.Contains(method) || node is not JsonObject operation)
                    continue;

                AddOperation(facts, $"operation {method.ToUpperInvariant()} {path}", operation);
            }
        }

        foreach (var (name, node) in schemas)
            AddSchema(facts, $"schema {name}", node!.AsObject(), schemas);

        foreach (var (name, scheme) in document["components"]?["securitySchemes"]?.AsObject() ?? [])
            facts[$"securityScheme {name}"] = Canonical(scheme);

        return facts;
    }

    private static void AddOperation(SortedDictionary<string, string> facts, string key, JsonObject operation)
    {
        facts[$"{key} | operationId"] = Text(operation["operationId"]);
        facts[$"{key} | summary"] = Text(operation["summary"]);
        facts[$"{key} | description"] = Text(operation["description"]);
        facts[$"{key} | tags"] = SortedList(operation["tags"]);
        facts[$"{key} | security"] = Canonical(operation["security"]);
        facts[$"{key} | deprecated"] = Text(operation["deprecated"]);

        foreach (var parameter in operation["parameters"]?.AsArray().OfType<JsonObject>() ?? [])
        {
            var parameterKey = $"{key} | parameter {Text(parameter["in"])}:{Text(parameter["name"])}";
            facts[$"{parameterKey} | required"] = Flag(parameter["required"]);
            facts[$"{parameterKey} | style"] = Text(parameter["style"]);
            facts[$"{parameterKey} | explode"] = Text(parameter["explode"]);
            facts[$"{parameterKey} | description"] = Text(parameter["description"]);
            AddValue(facts, parameterKey, parameter["schema"] as JsonObject);
            AddExtensions(facts, parameterKey, parameter);
        }

        if (operation["requestBody"] is JsonObject body)
        {
            var bodyKey = $"{key} | requestBody";
            facts[$"{bodyKey} | required"] = Flag(body["required"]);
            facts[$"{bodyKey} | description"] = Text(body["description"]);
            facts[$"{bodyKey} | content"] = SortedItems(body["content"]?.AsObject().Select(c => (JsonNode?)JsonValue.Create(c.Key)));
            foreach (var (mediaType, content) in body["content"]?.AsObject() ?? [])
            {
                var schema = content?["schema"] as JsonObject;
                AddValue(facts, $"{bodyKey} {mediaType}", schema);

                // A form is described inline, so each field is a fact of its own.
                if (schema?["properties"] is JsonObject fields)
                {
                    facts[$"{bodyKey} {mediaType} | required fields"] = SortedList(schema["required"]);
                    foreach (var (field, fieldSchema) in fields)
                        AddValue(facts, $"{bodyKey} {mediaType} field {field}", fieldSchema as JsonObject);
                }
            }
            AddExtensions(facts, bodyKey, body);
        }

        foreach (var (status, response) in operation["responses"]?.AsObject() ?? [])
        {
            var content = response?["content"]?.AsObject() ?? [];
            facts[$"{key} | response {status}"] = content.Count == 0
                ? "(no content)"
                : string.Join(", ", content.OrderBy(c => c.Key, StringComparer.Ordinal).Select(c => $"{c.Key} {Shape(c.Value?["schema"])}"));
        }

        AddExtensions(facts, key, operation);
    }

    private static void AddSchema(SortedDictionary<string, string> facts, string key, JsonObject schema, JsonObject schemas)
    {
        var parts = Parts(schema, schemas).ToList();
        var own = parts[^1];

        facts[$"{key} | exists"] = "yes";
        facts[$"{key} | type"] = Text(own["type"]) is var type && type != Absent ? type : (parts.Any(p => p["properties"] is not null) ? "object" : Absent);
        facts[$"{key} | bases"] = string.Join(", ", schema["allOf"]?.AsArray().Select(a => Text(a?["$ref"])).Where(r => r != Absent).Select(r => r[ComponentPrefix.Length..]) ?? []) is { Length: > 0 } bases ? bases : Absent;
        facts[$"{key} | description"] = Text(own["description"] ?? schema["description"]);
        facts[$"{key} | enum"] = Canonical(own["enum"]);
        facts[$"{key} | nullable"] = Text(own["nullable"] ?? schema["nullable"]);
        facts[$"{key} | additionalProperties"] = Canonical(own["additionalProperties"]);
        facts[$"{key} | discriminator"] = Canonical(own["discriminator"] ?? schema["discriminator"]);
        facts[$"{key} | oneOf"] = Canonical(own["oneOf"]);
        AddExtensions(facts, key, own);
        if (!ReferenceEquals(own, schema))
            AddExtensions(facts, key, schema);

        var required = parts.SelectMany(p => p["required"]?.AsArray().Select(r => (string)r!) ?? []).ToHashSet(StringComparer.Ordinal);
        foreach (var part in parts)
        {
            foreach (var (name, property) in part["properties"]?.AsObject() ?? [])
            {
                var propertyKey = $"{key} | property {name}";
                facts[$"{propertyKey} | required"] = required.Contains(name) ? "true" : "false";
                AddValue(facts, propertyKey, property as JsonObject);
                facts[$"{propertyKey} | description"] = Text(property?["description"]);
                AddExtensions(facts, propertyKey, property as JsonObject);
            }
        }
    }

    /// <summary>The facts of one value: a parameter's, a property's or a body's schema.</summary>
    private static void AddValue(SortedDictionary<string, string> facts, string key, JsonObject? schema)
    {
        facts[$"{key} | shape"] = Shape(schema);
        facts[$"{key} | nullable"] = Nullable(schema) ? "true" : "false";
        var value = Unwrap(schema);
        facts[$"{key} | enum"] = Canonical(value?["enum"]);
        facts[$"{key} | default"] = Canonical(value?["default"] ?? schema?["default"]);
        facts[$"{key} | deprecated"] = Text(schema?["deprecated"] ?? value?["deprecated"]);
        facts[$"{key} | readOnly"] = Text(schema?["readOnly"] ?? value?["readOnly"]);
        if (Text(value?["type"]) != "array")
            facts[$"{key} | items"] = Shape(value?["items"]);
        facts[$"{key} | example"] = Canonical(value?["example"] ?? schema?["example"]);
        foreach (var keyword in _validationKeywords)
            facts[$"{key} | {keyword}"] = Canonical(schema?[keyword] ?? value?[keyword]);
        AddExtensions(facts, key, value);
    }

    private static void AddExtensions(SortedDictionary<string, string> facts, string key, JsonObject? node)
    {
        foreach (var (name, value) in node ?? [])
        {
            if (name.StartsWith("x-", StringComparison.Ordinal))
                facts[$"{key} | {name}"] = Canonical(value);
        }
    }

    /// <summary>A schema's <c>allOf</c> bases, most distant first, then the schema's own part.</summary>
    private static IEnumerable<JsonObject> Parts(JsonObject schema, JsonObject schemas)
    {
        if (schema["allOf"] is not JsonArray allOf)
        {
            yield return schema;
            yield break;
        }

        JsonObject? own = null;
        foreach (var part in allOf.OfType<JsonObject>())
        {
            if (Text(part["$ref"]) is var reference && reference != Absent)
            {
                if (schemas[reference[ComponentPrefix.Length..]] is JsonObject baseSchema)
                {
                    foreach (var basePart in Parts(baseSchema, schemas))
                        yield return basePart;
                }
            }
            else
            {
                own = part;
            }
        }

        yield return own ?? schema;
    }

    /// <summary>A reference wrapped to carry <c>nullable</c> or a description, read as the reference.</summary>
    private static JsonObject? Unwrap(JsonObject? schema) =>
        schema is not null && Wrapped(schema) is { } inner ? Unwrap(inner) : schema;

    /// <summary>A value's type as one readable token: <c>string:date</c>, <c>ref:TeamDto</c>, <c>array&lt;integer:int32&gt;</c>.</summary>
    private static string Shape(JsonNode? node)
    {
        var schema = Unwrap(node as JsonObject);
        if (schema is null)
            return Absent;

        if (Text(schema["$ref"]) is var reference && reference != Absent)
            return "ref:" + reference[(reference.LastIndexOf('/') + 1)..];

        var type = Text(schema["type"]);
        if (type == "array")
            return $"array<{Shape(schema["items"])}>";

        if (schema["additionalProperties"] is JsonObject values && schema["properties"] is null)
            return $"map<{Shape(values)}>";

        if (schema["properties"] is JsonObject properties)
            return "object{" + string.Join(", ", properties.Select(p => $"{p.Key}: {Shape(p.Value)}")) + "}";

        foreach (var combinator in new[] { "oneOf", "anyOf", "allOf" })
        {
            if (schema[combinator] is JsonArray options)
                return $"{combinator}({string.Join(" | ", options.Select(Shape))})";
        }

        var format = Text(schema["format"]);
        return format == Absent ? type : $"{type}:{format}";
    }

    /// <summary>Whether the value may be null, said on the schema or on any wrapper between it and its reference.</summary>
    private static bool Nullable(JsonObject? schema)
    {
        for (var current = schema; current is not null; current = Wrapped(current))
        {
            if (current["nullable"]?.GetValueKind() == JsonValueKind.True)
                return true;
        }

        return false;
    }

    /// <summary>The one schema a single-element <c>allOf</c>, <c>oneOf</c> or <c>anyOf</c> wraps, if this is such a wrapper.</summary>
    private static JsonObject? Wrapped(JsonObject schema)
    {
        foreach (var combinator in new[] { "allOf", "oneOf", "anyOf" })
        {
            if (schema[combinator] is JsonArray { Count: 1 } single && single[0] is JsonObject inner && schema["properties"] is null)
                return inner;
        }

        return null;
    }

    private static string Text(JsonNode? node) => node switch
    {
        null => Absent,
        JsonValue value when value.GetValueKind() == JsonValueKind.String => value.GetValue<string>(),
        _ => node.ToJsonString(),
    };

    private static string Flag(JsonNode? node) => node?.GetValueKind() == JsonValueKind.True ? "true" : "false";

    private static string SortedItems(IEnumerable<JsonNode?>? nodes)
    {
        var items = nodes?.Select(Text).Order(StringComparer.Ordinal).ToList() ?? [];
        return items.Count == 0 ? Absent : string.Join(", ", items);
    }

    private static string SortedList(JsonNode? node) => SortedItems(node?.AsArray());

    /// <summary>JSON with object keys sorted, so key order never reads as a difference.</summary>
    public static string Canonical(JsonNode? node) => node is null ? Absent : Sort(node).ToJsonString();

    private static JsonNode? Sort(JsonNode? node) => node switch
    {
        JsonObject obj => new JsonObject(obj.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => KeyValuePair.Create(p.Key, Sort(p.Value)))),
        JsonArray array => new JsonArray([.. array.Select(Sort)]),
        // 100 and 100.0 are the same bound.
        JsonValue value when value.GetValueKind() == JsonValueKind.Number => JsonValue.Create(value.GetValue<decimal>() / 1.000000000000000000000000000000000m),
        null => null,
        _ => node.DeepClone(),
    };
}
