using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Wayd.Infrastructure.OpenApi;
using Wayd.Web.Api.IntegrationTests.Infrastructure;
using Wayd.Web.Api.IntegrationTests.OpenApi;
using Wayd.Web.Api.Mcp;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Holds the document <c>Microsoft.AspNetCore.OpenApi</c> serves to the one NSwag writes to
/// <c>specification.json</c>, which every generated client, the import templates and the MCP server are built
/// from. Every remaining difference must be one this class accepts and explains.
/// </summary>
/// <remarks>
/// Writes <c>openapi-parity-report.md</c> and the served document to the test output directory on every run.
/// </remarks>
[Collection(InMemoryApiTestCollection.Name)]
public sealed class OpenApiDocumentParityTests(WaydApiFactory factory)
{
    private const string DocumentPath = "/openapi/" + WaydOpenApiDocument.Name + ".json";

    private readonly WaydApiFactory _factory = factory;

    [Fact]
    public async Task MicrosoftDocument_DiffersFromNSwagsOnlyWhereAccepted()
    {
        // Arrange
        var nswag = await LoadNSwagDocument();
        using var client = _factory.CreateClient();
        var json = await client.GetStringAsync(DocumentPath, TestContext.Current.CancellationToken);
        var microsoft = JsonNode.Parse(json)!.AsObject();

        // Act
        var differences = OpenApiDocumentDiff.Compare(nswag, microsoft);
        var report = OpenApiDocumentDiff.Report(differences, Accepted(nswag), out var unexplained);

        // Assert
        var reportPath = Path.Combine(AppContext.BaseDirectory, "openapi-parity-report.md");
        await File.WriteAllTextAsync(reportPath, report, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "openapi-microsoft.json"), json, TestContext.Current.CancellationToken);
        Assert.True(unexplained.Count == 0, $"{unexplained.Count} differences are unexplained; see {reportPath}.");
    }

    [Fact]
    public async Task DocumentProvider_GivesTheServedDocument()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var served = JsonNode.Parse(await client.GetStringAsync(DocumentPath, TestContext.Current.CancellationToken))!.AsObject();
        var provider = _factory.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>(WaydOpenApiDocument.Name);

        // Act
        var document = await provider.GetOpenApiDocumentAsync(TestContext.Current.CancellationToken);
        var json = await document.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_0, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(OpenApiDocumentDiff.Compare(served, JsonNode.Parse(json)!.AsObject()));
    }

    [Fact]
    public async Task ToolInputSchemas_DifferOnlyWhereTheDocumentsAcceptablyDo()
    {
        // Arrange
        var nswag = await LoadNSwagDocument();
        using var client = _factory.CreateClient();
        var microsoft = JsonNode.Parse(await client.GetStringAsync(DocumentPath, TestContext.Current.CancellationToken))!.AsObject();
        var accepted = Accepted(nswag);
        var reasonsByOwner = OpenApiDocumentDiff.Compare(nswag, microsoft)
            .Select(d => (d.Owner, Accepted: OpenApiDocumentDiff.ReasonFor(d, accepted)))
            .Where(d => d.Accepted is { ReachesTools: true })
            .Select(d => (d.Owner, d.Accepted!.Reason))
            .GroupBy(d => d.Owner, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(d => d.Reason!).ToHashSet(), StringComparer.Ordinal);
        var microsoftOperations = Operations(microsoft);

        // Act
        var changed = new List<string>();
        var unexplained = new List<string>();
        foreach (var (owner, nswagOperation) in Operations(nswag))
        {
            if (!microsoftOperations.TryGetValue(owner, out var microsoftOperation)
                || ToolSchema(nswag, nswagOperation) == ToolSchema(microsoft, microsoftOperation))
            {
                continue;
            }

            var reasons = ReachedSchemas(nswag, nswagOperation).Concat(ReachedSchemas(microsoft, microsoftOperation))
                .Select(s => $"schema {s}")
                .Append(owner)
                .SelectMany(o => reasonsByOwner.GetValueOrDefault(o) ?? [])
                .Distinct()
                .ToList();
            changed.Add(owner);
            changed.AddRange(reasons.Select(r => $"  - {r}"));
            if (reasons.Count == 0)
                unexplained.Add(owner);
        }

        // Assert
        await File.WriteAllLinesAsync(Path.Combine(AppContext.BaseDirectory, "openapi-tool-schema-changes.txt"), changed, TestContext.Current.CancellationToken);
        Assert.Empty(unexplained);
    }

    private static async Task<JsonObject> LoadNSwagDocument() =>
        JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "OpenApi", "specification.json"), TestContext.Current.CancellationToken))!.AsObject();

    /// <summary>Every operation, keyed as <see cref="OpenApiFacts"/> keys it.</summary>
    private static Dictionary<string, JsonObject> Operations(JsonObject document) =>
        document["paths"]!.AsObject()
            .SelectMany(p => p.Value!.AsObject().Where(o => o.Value is JsonObject { } op && op["responses"] is not null)
                .Select(o => (Key: $"operation {o.Key.ToUpperInvariant()} {p.Key}", Operation: o.Value!.AsObject())))
            .ToDictionary(o => o.Key, o => o.Operation, StringComparer.Ordinal);

    /// <summary>The MCP input schema <see cref="OpenApiToolSchema"/> builds for an operation, in a comparable form.</summary>
    private static string ToolSchema(JsonObject document, JsonObject operation)
    {
        try
        {
            var (inputSchema, arguments) = new OpenApiToolSchema(document).Build(operation);
            return OpenApiFacts.Canonical(SortRequired(inputSchema)) + string.Join(",", arguments.OrderBy(a => a.Name, StringComparer.Ordinal));
        }
        catch (NotSupportedException)
        {
            return "(not a tool)";
        }
    }

    /// <summary>The schema with every <c>required</c> list in order, which is not part of its meaning.</summary>
    private static JsonNode? SortRequired(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj.ToList())
                {
                    obj[key] = key == "required" && value is JsonArray names
                        ? new JsonArray([.. names.Select(n => (string)n!).Order(StringComparer.Ordinal).Select(n => (JsonNode)JsonValue.Create(n))])
                        : SortRequired(value?.DeepClone());
                }

                return obj;
            case JsonArray array:
                return new JsonArray([.. array.Select(i => SortRequired(i?.DeepClone()))]);
            default:
                return node;
        }
    }

    /// <summary>
    /// The component schemas an operation's parameters and body reach, directly or through other components:
    /// everything its tool input schema is built from.
    /// </summary>
    private static HashSet<string> ReachedSchemas(JsonObject document, JsonObject operation)
    {
        var schemas = document["components"]?["schemas"]?.AsObject() ?? [];
        var reached = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<JsonNode?>([operation["parameters"], operation["requestBody"]]);
        while (pending.TryPop(out var node))
        {
            switch (node)
            {
                case JsonObject obj:
                    if ((string?)(obj["$ref"] as JsonValue) is { } reference && reached.Add(reference[(reference.LastIndexOf('/') + 1)..]))
                        pending.Push(schemas[reference[(reference.LastIndexOf('/') + 1)..]]);
                    foreach (var (_, value) in obj)
                        pending.Push(value);
                    break;
                case JsonArray array:
                    foreach (var item in array)
                        pending.Push(item);
                    break;
            }
        }

        return reached;
    }

    /// <summary>The differences that are intended, each with the reason it is.</summary>
    private static List<AcceptedDifference> Accepted(JsonObject nswag)
    {
        var nswagFacts = OpenApiFacts.Extract(nswag);

        bool IsJsonPatch(string owner) =>
            owner is "schema Operation" or "schema OperationBase"
            || owner.StartsWith("schema OperationOf", StringComparison.Ordinal)
            || owner.StartsWith("schema JsonPatchDocument", StringComparison.Ordinal);

        // What NSwag says a value's type is, for the fact at the same location.
        string NSwagShape(OpenApiDifference d) =>
            nswagFacts.GetValueOrDefault(d.Location[..d.Location.LastIndexOf(" | ", StringComparison.Ordinal)] + " | shape", OpenApiFacts.Absent);

        return
        [
            new("JSON Patch bodies. NSwag describes `JsonPatchDocument<T>` as an object with an `operations` list, which is not what the endpoint reads; Microsoft.AspNetCore.OpenApi describes the RFC 6902 array as one `JsonPatchDocument` schema. The client sends patches without the generated methods because NSwag's shape was wrong.",
                d => IsJsonPatch(d.Owner) || d.Expected.Contains("ref:JsonPatchDocumentOf", StringComparison.Ordinal)),
            new("`NotEmpty()` on a value that is not a string. NSwag published it as `minLength: 1`, which says nothing about a date, a Guid or a list; MicroElements' Microsoft.AspNetCore.OpenApi rules publish `minItems: 1` on a list and nothing on a date.",
                d => (d.Kind == "minLength" && d.Expected == "1" && d.Actual == OpenApiFacts.Absent && NSwagShape(d) != "string")
                    || (d.Kind == "minItems" && d.Expected == OpenApiFacts.Absent && d.Actual == "1" && NSwagShape(d).StartsWith("array<", StringComparison.Ordinal))),
            new("`minLength: 0`, which NSwag's rules published for a maximum length and which allows every string.",
                d => d.Kind == "minLength" && d.Expected == "0" && d.Actual == OpenApiFacts.Absent),
            new("A Guid is published as format `uuid`, the registered JSON Schema format, instead of NSwag's `guid`. NSwag's TypeScript generator maps both to `string`.",
                d => (d.Kind == "shape" || d.Kind.StartsWith("response ", StringComparison.Ordinal))
                    && d.Expected.Replace(":guid", ":uuid", StringComparison.Ordinal) == d.Actual,
                ReachesTools: false),
            new("NSwag's Guid mapper gave every Guid the example `00000000-0000-0000-0000-000000000000`, which tells a reader nothing. No client generator reads examples.",
                d => d.Kind == "example" && d.Expected == "\"00000000-0000-0000-0000-000000000000\"" && d.Actual == OpenApiFacts.Absent,
                ReachesTools: false),
            new("`x-enumNames` is NSwag's own extension. Every enum is a string enum whose values are already its names, so NSwag's TypeScript generator names the members from the values.",
                d => d.Kind == "x-enumNames" && d.Actual == OpenApiFacts.Absent,
                ReachesTools: false),
            new("An enum with no XML documentation. NSwag gives it an empty description; Microsoft.AspNetCore.OpenApi gives it none.",
                d => d.Kind == "description" && d.Expected == "" && d.Actual == OpenApiFacts.Absent,
                ReachesTools: false),
            new("`EmailAddress()`. MicroElements' NSwag rules publish it as a `pattern`; its Microsoft.AspNetCore.OpenApi rules as `format: email`. Both generators type the property as a string.",
                d => (d.Kind == "pattern" && d.Expected == "\"^[^@]\\u002B@[^@]\\u002B$\"" && d.Actual == OpenApiFacts.Absent)
                    || (d.Kind == "shape" && d.Expected == "string" && d.Actual == "string:email")),
            new("`NotEmpty()` beside a longer `MinimumLength()`. MicroElements' NSwag rules publish the `minLength: 1` of `NotEmpty()`; its Microsoft.AspNetCore.OpenApi rules publish the stricter bound, which is the one the validator enforces.",
                d => d.Kind == "minLength" && d.Expected == "1" && int.TryParse(d.Actual, out var bound) && bound > 1),
            new("`RuleForEach(...).MaximumLength()`. NSwag published the bound on each item as a `maxLength` on the list itself, which no list has; Microsoft.AspNetCore.OpenApi publishes nothing.",
                d => d.Kind == "maxLength" && d.Actual == OpenApiFacts.Absent && NSwagShape(d).StartsWith("array<", StringComparison.Ordinal)),
            new("A record's constructor defaults (`ApplicationPermission(..., string Category = \"\", bool IsRoot = false)`), which Microsoft.AspNetCore.OpenApi publishes as the properties' `default`. The TypeScript client generates interfaces, which carry no defaults.",
                d => d.Kind == "default" && d.Expected == OpenApiFacts.Absent && d.Owner == "schema ApplicationPermission"),
            new("`[JsonExtensionData]` on `ProblemDetails`. NSwag publishes the extra properties as `additionalProperties: { nullable: true }`, Microsoft.AspNetCore.OpenApi as `{}`, because OpenAPI 3.0 has no nullable schema without a type; both allow any value, and NSwag's TypeScript generator writes `[key: string]: any` for either.",
                d => d.Kind == "additionalProperties" && d.Expected == "{\"nullable\":true}" && d.Actual == "{}"),
            new("`style: form` and `explode: true` on a list in the query string. They are OpenAPI's defaults for a query parameter, which NSwag states and Microsoft.OpenApi's writer leaves out; NSwag's TypeScript generator repeats the name for each value (`?status=A&status=B`) either way.",
                d => d.Kind is "style" or "explode" && d.Expected is "form" or "true" && d.Actual == OpenApiFacts.Absent),
            new("A property declared on a derived class. NSwag's required and FluentValidation processors never saw these, because NSwag had already moved them into an `allOf` part; they are required and carry their validation rules here as on any other class, so the TypeScript client stops typing the non-nullable ones as optional.",
                d => d.Owner.StartsWith("schema ", StringComparison.Ordinal)
                    && nswagFacts.GetValueOrDefault($"{d.Owner} | bases", OpenApiFacts.Absent) != OpenApiFacts.Absent
                    && ((d.Kind == "required" && d.Expected == "false" && d.Actual == "true")
                        || (d.Kind is "minLength" or "maxLength" or "minimum" or "maximum" or "pattern" && d.Expected == OpenApiFacts.Absent))),
        ];
    }
}
