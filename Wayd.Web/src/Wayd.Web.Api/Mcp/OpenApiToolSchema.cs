using System.Text.Json;
using System.Text.Json.Nodes;

namespace Wayd.Web.Api.Mcp;

/// <summary>
/// Turns an operation in the API's OpenAPI 3.0 document into a tool's input schema (JSON Schema 2020-12):
/// one property per path, query or header parameter, plus <c>requestBody</c> for a JSON body. Its success
/// response becomes the tool's output schema.
/// </summary>
/// <remarks>
/// References are inlined, since not every client resolves <c>$ref</c>; a type that contains itself is cut
/// to a plain object where it recurs. OpenAPI 3.0's <c>nullable</c> and boolean exclusive bounds have no
/// meaning in 2020-12 and are rewritten, and vendor extensions and examples are dropped. A type with
/// subtypes becomes <c>anyOf</c> its subtypes, each with its discriminator value, because the document
/// describes the base type alone and closes it to the subtypes' properties.
/// </remarks>
public sealed class OpenApiToolSchema
{
    /// <summary>The argument that carries an operation's JSON request body.</summary>
    public const string RequestBodyArgument = "requestBody";

    private static readonly HashSet<string> _inputKeywords =
    [
        "type", "format", "description", "properties", "required", "items", "enum", "default",
        "minimum", "maximum", "minLength", "maxLength", "pattern", "minItems", "maxItems", "uniqueItems",
        "additionalProperties",
    ];

    /// <summary>
    /// The shape of a response without the request validation rules its model shares. The API does not
    /// hold what it returns to them, and a client may reject a result that breaks its schema: a record
    /// synced from another system with an empty name would otherwise fail every list holding it.
    /// </summary>
    private static readonly HashSet<string> _outputKeywords =
    [
        "type", "format", "description", "properties", "required", "items", "enum", "additionalProperties",
    ];

    private readonly JsonObject _schemas;

    /// <param name="document">The whole OpenAPI document, whose components the operation refers to.</param>
    public OpenApiToolSchema(JsonObject document)
    {
        _schemas = document["components"]?["schemas"] as JsonObject ?? [];
    }

    /// <summary>The input schema for <paramref name="operation"/> and where each argument is sent.</summary>
    /// <exception cref="NotSupportedException">The operation takes a body other than JSON.</exception>
    public (JsonObject InputSchema, IReadOnlyList<ToolArgument> Arguments) Build(JsonObject operation)
    {
        var properties = new JsonObject();
        var required = new JsonArray();
        var arguments = new List<ToolArgument>();

        foreach (var parameter in operation["parameters"]?.AsArray().OfType<JsonObject>() ?? [])
        {
            var name = (string)parameter["name"]!;
            var location = (string)parameter["in"]! switch
            {
                "path" => ArgumentLocation.Path,
                "query" => ArgumentLocation.Query,
                "header" => ArgumentLocation.Header,
                var other => throw new NotSupportedException($"Parameter '{name}' is sent in '{other}', which a tool cannot send."),
            };

            // A query parameter bound from an options object keeps its property's PascalCase name. The model
            // binder matches query keys case-insensitively, so the argument takes the camelCase every other
            // argument uses.
            if (location == ArgumentLocation.Query)
                name = JsonNamingPolicy.CamelCase.ConvertName(name);

            var schema = Convert(parameter["schema"] as JsonObject ?? [], [], _inputKeywords);
            if (parameter["description"] is JsonValue description && !string.IsNullOrWhiteSpace((string?)description))
                schema["description"] = (string)description!;

            properties[name] = schema;
            arguments.Add(new ToolArgument(name, location));
            if (location == ArgumentLocation.Path || parameter["required"]?.GetValue<bool>() == true)
                required.Add(name);
        }

        if (operation["requestBody"] is JsonObject body)
        {
            var content = body["content"] as JsonObject ?? [];
            if (content["application/json"]?["schema"] is not JsonObject bodySchema)
                throw new NotSupportedException($"The request body is sent as {string.Join(", ", content.Select(c => c.Key))}; a tool can send only JSON.");

            properties[RequestBodyArgument] = Convert(bodySchema, [], _inputKeywords);
            arguments.Add(new ToolArgument(RequestBodyArgument, ArgumentLocation.Body));
            if (body["required"]?.GetValue<bool>() == true)
                required.Add(RequestBodyArgument);
        }

        var inputSchema = new JsonObject { ["type"] = "object", ["properties"] = properties };
        if (required.Count > 0)
            inputSchema["required"] = required;

        return (inputSchema, arguments);
    }

    /// <summary>
    /// The output schema for <paramref name="operation"/>, from its first success response with a JSON body,
    /// or null when no success response has one.
    /// </summary>
    public ToolOutput? BuildOutput(JsonObject operation)
    {
        var responseSchema = (operation["responses"] as JsonObject ?? [])
            .Where(r => r.Key.StartsWith('2'))
            .OrderBy(r => r.Key, StringComparer.Ordinal)
            .Select(r => r.Value?["content"]?["application/json"]?["schema"] as JsonObject)
            .FirstOrDefault(s => s is not null);

        return responseSchema is null ? null : ToolOutput.For(Convert(responseSchema, [], _outputKeywords));
    }

    private JsonObject Convert(JsonObject source, HashSet<string> resolving, HashSet<string> kept)
    {
        var merged = Combine(Flatten(source, resolving, out var referenced));
        try
        {
            if (merged["discriminator"] is JsonObject { } discriminator && discriminator["mapping"] is JsonObject mapping)
                return Subtypes(merged, (string)discriminator["propertyName"]!, mapping, resolving, kept);

            var result = new JsonObject();
            foreach (var (key, value) in merged)
            {
                if (!kept.Contains(key) || value is null)
                    continue;

                result[key] = key switch
                {
                    "properties" => new JsonObject(value.AsObject().Select(p =>
                        KeyValuePair.Create(p.Key, (JsonNode?)Convert(p.Value as JsonObject ?? [], resolving, kept)))),
                    "items" => Convert(value as JsonObject ?? [], resolving, kept),
                    "additionalProperties" when value is JsonObject schema => Convert(schema, resolving, kept),
                    _ => value.DeepClone(),
                };
            }

            if (result["description"] is JsonValue text && string.IsNullOrWhiteSpace((string?)text))
                result.Remove("description");

            if (kept.Contains("minimum"))
            {
                ApplyExclusiveBound(merged, result, "exclusiveMinimum", "minimum");
                ApplyExclusiveBound(merged, result, "exclusiveMaximum", "maximum");
            }

            if (merged["nullable"]?.GetValue<bool>() == true)
                AllowNull(result);

            return result;
        }
        finally
        {
            if (referenced is not null)
                resolving.Remove(referenced);
        }
    }

    /// <summary>
    /// A base type as <c>anyOf</c> the subtypes its discriminator maps, each with the discriminator fixed to
    /// the subtype's value.
    /// </summary>
    private JsonObject Subtypes(JsonObject baseSchema, string propertyName, JsonObject mapping, HashSet<string> resolving, HashSet<string> kept)
    {
        var nullable = baseSchema["nullable"]?.GetValue<bool>() == true;
        var variants = new JsonArray();
        foreach (var (value, reference) in mapping)
        {
            var name = NameOf((string)reference!);
            var variant = Combine(_schemas[name] as JsonObject ?? []);
            variant.Remove("description");
            variant.Remove("nullable");
            var properties = variant["properties"] as JsonObject ?? [];
            properties[propertyName] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray(value) };
            variant["properties"] = properties;
            var required = variant["required"] as JsonArray ?? [];
            required.Add(propertyName);
            variant["required"] = required;

            var added = resolving.Add(name);
            try
            {
                variants.Add(Convert(variant, resolving, kept));
            }
            finally
            {
                if (added)
                    resolving.Remove(name);
            }
        }

        if (nullable)
            variants.Add(new JsonObject { ["type"] = "null" });

        var result = new JsonObject
        {
            ["type"] = nullable ? new JsonArray("object", "null") : "object",
            ["anyOf"] = variants,
        };
        if (baseSchema["description"] is JsonValue description && !string.IsNullOrWhiteSpace((string?)description))
            result["description"] = (string)description!;
        return result;
    }

    /// <summary>
    /// A type written as <c>allOf</c> its base type and its own part, as one schema: the parts' properties
    /// and required lists combine, and for every other keyword a later part wins over an earlier one.
    /// </summary>
    /// <remarks>
    /// A base type's discriminator is dropped, and so is its discriminator property: the combined schema is
    /// one particular subtype, and System.Text.Json writes <c>$type</c> only where the declared type is the
    /// base type, which <see cref="Subtypes"/> describes instead. A schema without several parts is returned
    /// as it is.
    /// </remarks>
    private JsonObject Combine(JsonObject schema)
    {
        if (schema["allOf"] is not JsonArray { Count: > 1 } parts)
            return schema;

        var combined = new JsonObject();
        var properties = new JsonObject();
        var required = new JsonArray();
        var resolvedParts = parts.OfType<JsonObject>()
            .Select(p => Combine((string?)p["$ref"] is { } reference ? _schemas[NameOf(reference)] as JsonObject ?? [] : p))
            .Append(schema);

        var discriminators = new List<string>();
        foreach (var part in resolvedParts)
        {
            foreach (var (key, value) in part)
            {
                switch (key)
                {
                    case "allOf":
                        break;
                    case "discriminator":
                        if ((string?)value?["propertyName"] is { } propertyName)
                            discriminators.Add(propertyName);
                        break;
                    case "properties":
                        foreach (var (name, property) in value?.AsObject() ?? [])
                            properties[name] = property?.DeepClone();
                        break;
                    case "required":
                        foreach (var name in value?.AsArray() ?? [])
                        {
                            if (!required.Any(r => (string?)r == (string?)name))
                                required.Add(name?.DeepClone());
                        }
                        break;
                    case "description" when string.IsNullOrWhiteSpace((string?)(value as JsonValue)):
                        break;
                    default:
                        combined[key] = value?.DeepClone();
                        break;
                }
            }
        }

        foreach (var propertyName in discriminators)
        {
            properties.Remove(propertyName);
            foreach (var entry in required.Where(r => (string?)r == propertyName).ToList())
                required.Remove(entry);
        }

        if (properties.Count > 0)
            combined["properties"] = properties;
        if (required.Count > 0)
            combined["required"] = required;
        return combined;
    }

    /// <summary>The component a <c>$ref</c> names.</summary>
    private static string NameOf(string reference) => reference[(reference.LastIndexOf('/') + 1)..];

    /// <summary>
    /// Resolves a reference, and the single-entry <c>oneOf</c>/<c>allOf</c> wrapper around one, into one
    /// schema; keywords written beside the reference win over the referenced type's.
    /// </summary>
    private JsonObject Flatten(JsonObject source, HashSet<string> resolving, out string? referenced)
    {
        referenced = null;
        var wrapped = (source["oneOf"] ?? source["allOf"]) as JsonArray;
        var inner = wrapped is { Count: 1 } ? wrapped[0] as JsonObject : null;
        var reference = (string?)(inner ?? source)["$ref"];

        if (reference is null)
            return inner is null ? source : Merge(Flatten(inner, resolving, out referenced), source);

        var name = reference[(reference.LastIndexOf('/') + 1)..];
        if (_schemas[name] is not JsonObject target || !resolving.Add(name))
            return Merge(new JsonObject { ["type"] = "object" }, source);

        referenced = name;
        return Merge(target, source);
    }

    private static JsonObject Merge(JsonObject baseSchema, JsonObject overrides)
    {
        var merged = new JsonObject();
        foreach (var (key, value) in baseSchema)
            merged[key] = value?.DeepClone();
        foreach (var (key, value) in overrides)
        {
            if (key is "$ref" or "oneOf" or "allOf")
                continue;
            if (key == "description" && string.IsNullOrWhiteSpace((string?)(value as JsonValue)))
                continue;
            merged[key] = value?.DeepClone();
        }
        return merged;
    }

    private static void ApplyExclusiveBound(JsonObject source, JsonObject result, string exclusiveKeyword, string boundKeyword)
    {
        if (source[exclusiveKeyword] is not JsonValue exclusive)
            return;

        if (exclusive.TryGetValue<bool>(out var isExclusive))
        {
            if (isExclusive && result[boundKeyword] is JsonNode bound)
            {
                result.Remove(boundKeyword);
                result[exclusiveKeyword] = bound;
            }
        }
        else
        {
            result[exclusiveKeyword] = exclusive.DeepClone();
        }
    }

    private static void AllowNull(JsonObject schema)
    {
        switch (schema["type"])
        {
            case JsonValue single:
                schema["type"] = new JsonArray((string)single!, "null");
                break;
            case JsonArray types when !types.Any(t => (string?)t == "null"):
                types.Add("null");
                break;
        }

        if (schema["enum"] is JsonArray values && !values.Any(v => v is null))
            values.Add(null);
    }
}

/// <summary>Where a tool argument goes in the request it is sent as.</summary>
public enum ArgumentLocation
{
    /// <summary>Substituted into the route.</summary>
    Path,

    /// <summary>Sent in the query string, once per item for an array.</summary>
    Query,

    /// <summary>Sent as a request header.</summary>
    Header,

    /// <summary>Sent as the JSON request body.</summary>
    Body,
}

/// <summary>The shape of a tool's structured result, and whether the response is wrapped to fit it.</summary>
/// <param name="Schema">The output schema, always an object.</param>
/// <param name="IsWrapped">
/// Whether the response sits under <see cref="ResultProperty"/> rather than being the result itself.
/// </param>
public sealed record ToolOutput(JsonObject Schema, bool IsWrapped)
{
    /// <summary>The property that holds a response that is not a JSON object.</summary>
    public const string ResultProperty = "result";

    /// <summary>
    /// The output for a response described by <paramref name="responseSchema"/>. A response that may be
    /// anything but an object, null included, is wrapped.
    /// </summary>
    /// <remarks>
    /// Protocol versions before <c>2026-07-28</c> accept only an object output schema and an object as
    /// structured content. Wrapping for every client keeps one shape rather than one per negotiated version.
    /// </remarks>
    public static ToolOutput For(JsonObject responseSchema) =>
        (string?)(responseSchema["type"] as JsonValue) == "object"
            ? new ToolOutput(responseSchema, false)
            : new ToolOutput(
                new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject { [ResultProperty] = responseSchema },
                    ["required"] = new JsonArray(ResultProperty),
                },
                true);
}

/// <summary>A tool argument and where it is sent.</summary>
/// <param name="Name">The argument name, as the operation names the parameter.</param>
/// <param name="Location">Where the argument goes in the request.</param>
public sealed record ToolArgument(string Name, ArgumentLocation Location);
