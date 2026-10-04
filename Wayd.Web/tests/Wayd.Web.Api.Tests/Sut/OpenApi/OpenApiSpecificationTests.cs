using System.Text.Json;
using FluentAssertions;

namespace Wayd.Web.Api.Tests.Sut.OpenApi;

/// <summary>
/// Holds the published OpenAPI document to naming every value of a closed set.
/// </summary>
/// <remarks>
/// A closed set (a status, state, role or category the domain branches on) goes over the wire as its
/// stable code, which the document lists. A bare integer gives a caller nothing to choose from — agents
/// reading the document guess or call a lookup first — and ties every caller to the enum's numbering.
/// Runs over the checked-in document, which a Debug build of the API regenerates.
/// </remarks>
public sealed class OpenApiSpecificationTests
{
    /// <summary>
    /// Integer fields whose names read like a closed set but are not one.
    /// </summary>
    private static readonly HashSet<string> _notClosedSets =
    [
        // The HTTP status code.
        "schema ProblemDetails.status",
        // A count of results, not a category.
        "parameter Search_Search.maxResultsPerCategory",
        // Mirrors the stage's embedded { id, name } status, which still travels as an id.
        "schema UpdateProjectStageRequest.status",
    ];

    private static readonly string[] _closedSetSuffixes =
        ["status", "statuses", "state", "states", "role", "roles", "category", "categories", "kind", "kinds"];

    private static JsonElement LoadDocument()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "OpenApi", "specification.json");
        using var stream = File.OpenRead(path);
        return JsonDocument.Parse(stream).RootElement.Clone();
    }

    [Fact]
    public void EveryEnumSchema_ListsItsValuesByName()
    {
        // Arrange
        var schemas = LoadDocument().GetProperty("components").GetProperty("schemas");

        // Act
        var integerEnums = schemas.EnumerateObject()
            .Where(s => s.Value.TryGetProperty("enum", out _)
                && (!s.Value.TryGetProperty("type", out var type) || type.GetString() != "string"))
            .Select(s => s.Name)
            .ToList();

        // Assert
        integerEnums.Should().BeEmpty("an enum should publish the names callers send, not its numbering");
    }

    [Fact]
    public void ClosedSetFields_AreNotBareIntegers()
    {
        // Arrange
        var document = LoadDocument();

        // Act
        var offenders = ParameterFields(document)
            .Concat(SchemaFields(document))
            .Where(f => IsInteger(f.Schema) && LooksLikeClosedSet(f.Name))
            .Select(f => f.Label)
            .Where(label => !_notClosedSets.Contains(label))
            .ToList();

        // Assert
        offenders.Should().BeEmpty(
            "a status, state, role or category should take the enum so the document names its values; " +
            "an integer field that is not a closed set belongs in the allow-list");
    }

    private static IEnumerable<(string Label, string Name, JsonElement Schema)> ParameterFields(JsonElement document)
    {
        foreach (var path in document.GetProperty("paths").EnumerateObject())
        foreach (var operation in path.Value.EnumerateObject())
        {
            // A path item also holds path-level fields (parameters, summary, servers) beside its operations.
            if (!operation.Value.TryGetProperty("operationId", out var operationIdProperty)
                || !operation.Value.TryGetProperty("parameters", out var parameters))
                continue;

            var operationId = operationIdProperty.GetString();
            foreach (var parameter in parameters.EnumerateArray())
            {
                var name = parameter.GetProperty("name").GetString()!;
                if (parameter.TryGetProperty("schema", out var schema))
                    yield return ($"parameter {operationId}.{name}", name, schema);
            }
        }
    }

    private static IEnumerable<(string Label, string Name, JsonElement Schema)> SchemaFields(JsonElement document)
    {
        foreach (var schema in document.GetProperty("components").GetProperty("schemas").EnumerateObject())
        {
            IEnumerable<JsonElement> parts = schema.Value.TryGetProperty("allOf", out var allOf)
                ? allOf.EnumerateArray().Append(schema.Value)
                : [schema.Value];

            foreach (var part in parts)
            {
                if (!part.TryGetProperty("properties", out var properties))
                    continue;

                foreach (var property in properties.EnumerateObject())
                    yield return ($"schema {schema.Name}.{property.Name}", property.Name, property.Value);
            }
        }
    }

    private static bool IsInteger(JsonElement schema)
    {
        if (!schema.TryGetProperty("type", out var type))
            return false;

        return type.GetString() switch
        {
            "integer" => true,
            "array" => schema.TryGetProperty("items", out var items) && IsInteger(items),
            _ => false,
        };
    }

    private static bool LooksLikeClosedSet(string name) =>
        _closedSetSuffixes.Any(suffix => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
}
