using System.Text.Json;
using System.Text.Json.Nodes;

namespace Wayd.Web.Api.Mcp;

/// <summary>
/// Turns an operation in the API's OpenAPI 3.0 document into a tool's input schema (JSON Schema 2020-12):
/// one property per path, query or header parameter, plus <c>requestBody</c> for a JSON body.
/// </summary>
/// <remarks>
/// References are inlined, since not every client resolves <c>$ref</c>; a type that contains itself is cut
/// to a plain object where it recurs. OpenAPI 3.0's <c>nullable</c> and boolean exclusive bounds have no
/// meaning in 2020-12 and are rewritten; NSwag's <c>guid</c> format becomes the standard <c>uuid</c>, and
/// vendor extensions and examples are dropped.
/// </remarks>
public sealed class OpenApiToolSchema
{
    /// <summary>The argument that carries an operation's JSON request body.</summary>
    public const string RequestBodyArgument = "requestBody";

    private static readonly HashSet<string> _keptKeywords =
    [
        "type", "format", "description", "properties", "required", "items", "enum", "default",
        "minimum", "maximum", "minLength", "maxLength", "pattern", "minItems", "maxItems", "uniqueItems",
        "additionalProperties",
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

            var schema = Convert(parameter["schema"] as JsonObject ?? [], []);
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

            properties[RequestBodyArgument] = Convert(bodySchema, []);
            arguments.Add(new ToolArgument(RequestBodyArgument, ArgumentLocation.Body));
            if (body["required"]?.GetValue<bool>() == true)
                required.Add(RequestBodyArgument);
        }

        var inputSchema = new JsonObject { ["type"] = "object", ["properties"] = properties };
        if (required.Count > 0)
            inputSchema["required"] = required;

        return (inputSchema, arguments);
    }

    private JsonObject Convert(JsonObject source, HashSet<string> resolving)
    {
        var merged = Flatten(source, resolving, out var referenced);
        try
        {
            var result = new JsonObject();
            foreach (var (key, value) in merged)
            {
                if (!_keptKeywords.Contains(key) || value is null)
                    continue;

                result[key] = key switch
                {
                    "properties" => new JsonObject(value.AsObject().Select(p =>
                        KeyValuePair.Create(p.Key, (JsonNode?)Convert(p.Value as JsonObject ?? [], resolving)))),
                    "items" => Convert(value as JsonObject ?? [], resolving),
                    "additionalProperties" when value is JsonObject schema => Convert(schema, resolving),
                    "format" when (string?)value == "guid" => "uuid",
                    _ => value.DeepClone(),
                };
            }

            if (result["description"] is JsonValue text && string.IsNullOrWhiteSpace((string?)text))
                result.Remove("description");

            ApplyExclusiveBound(merged, result, "exclusiveMinimum", "minimum");
            ApplyExclusiveBound(merged, result, "exclusiveMaximum", "maximum");

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
    /// Resolves a reference, and NSwag's single-entry <c>oneOf</c>/<c>allOf</c> wrapper around one, into one
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

/// <summary>A tool argument and where it is sent.</summary>
/// <param name="Name">The argument name, as the operation names the parameter.</param>
/// <param name="Location">Where the argument goes in the request.</param>
public sealed record ToolArgument(string Name, ArgumentLocation Location);
