using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Web.Api.IntegrationTests.Infrastructure;
using Wayd.Web.Api.Mcp;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Compares the hosted MCP server's tools with the @wayd/mcp package's, through a snapshot that package's
/// own tests keep current (<c>Mcp/npm-tool-parity.json</c>). Tool names are a contract with skills and
/// prompts already in use, so the hosted server must offer every one, advertise the same title and
/// annotations, accept every argument callers send today, and require nothing they may leave out.
/// </summary>
/// <remarks>
/// Builds the catalogue from the booted host because it is described from the API's runtime OpenAPI
/// document, which only the whole application can produce.
/// </remarks>
[Collection(InMemoryApiTestCollection.Name)]
public sealed class McpToolCatalogTests(WaydApiFactory factory)
{
    private static readonly JsonObject _snapshot = JsonNode.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Mcp", "npm-tool-parity.json")))!.AsObject();

    private readonly WaydApiFactory _factory = factory;

    [Fact]
    public async Task GetTools_OffersExactlyTheNpmServersToolNames()
    {
        // Arrange
        var expected = SnapshotTools().Select(t => (string)t["name"]!).Order(StringComparer.Ordinal).ToList();

        // Act
        var tools = await GetTools();

        // Assert
        Assert.Equal(expected, tools.Select(t => t.ProtocolTool.Name).Order(StringComparer.Ordinal).ToList());
    }

    [Fact]
    public async Task GetTools_AdvertisesTheNpmServersTitlesAndAnnotations()
    {
        // Arrange
        var tools = (await GetTools()).ToDictionary(t => t.ProtocolTool.Name);

        // Act
        var mismatches = SnapshotTools()
            .Where(t => tools.ContainsKey((string)t["name"]!))
            .Select(t =>
            {
                var tool = tools[(string)t["name"]!].ProtocolTool;
                var annotations = t["annotations"]!;
                var expected = $"title={t["title"]}/{t["title"]} readOnly={annotations["readOnlyHint"]} destructive={annotations["destructiveHint"]} idempotent={annotations["idempotentHint"]} openWorld={annotations["openWorldHint"]}";
                var actual = $"title={tool.Title}/{tool.Annotations?.Title} readOnly={Hint(tool.Annotations?.ReadOnlyHint)} destructive={Hint(tool.Annotations?.DestructiveHint)} idempotent={Hint(tool.Annotations?.IdempotentHint)} openWorld={Hint(tool.Annotations?.OpenWorldHint)}";
                return (tool.Name, Expected: expected, Actual: actual);
            })
            .Where(m => m.Expected != m.Actual)
            .Select(m => $"{m.Name}: expected '{m.Expected}', got '{m.Actual}'")
            .ToList();

        // Assert
        Assert.Empty(mismatches);
    }

    [Fact]
    public async Task GetTools_AcceptsEveryArgumentTheNpmServerTakesAndRequiresNoMore()
    {
        // Arrange
        var tools = (await GetTools()).ToDictionary(t => t.ProtocolTool.Name);

        // Act
        var mismatches = new List<string>();
        foreach (var expected in SnapshotTools().Where(t => tools.ContainsKey((string)t["name"]!)))
        {
            var name = (string)expected["name"]!;
            var schema = tools[name].ProtocolTool.InputSchema;
            var arguments = schema.TryGetProperty("properties", out var properties)
                ? properties.EnumerateObject().Select(p => p.Name).ToHashSet()
                : [];
            var required = schema.TryGetProperty("required", out var requiredArray)
                ? requiredArray.EnumerateArray().Select(r => r.GetString()!).ToHashSet()
                : [];

            var npmArguments = expected["arguments"]!.AsArray().Select(a => (string)a!).ToHashSet();
            var npmRequired = expected["required"]!.AsArray().Select(a => (string)a!).ToHashSet();

            foreach (var missing in npmArguments.Except(arguments))
                mismatches.Add($"{name}: does not take '{missing}'");
            foreach (var extra in required.Except(npmRequired))
                mismatches.Add($"{name}: requires '{extra}', which the npm server does not");
        }

        // Assert
        Assert.Empty(mismatches);
    }

    [Fact]
    public void ServerInstructions_MatchTheNpmServers()
    {
        // Arrange
        var expected = (string)_snapshot["instructions"]!;

        // Act
        var instructions = McpServerInstructions.Text;

        // Assert
        Assert.Equal(expected, instructions);
    }

    [Fact]
    public async Task GetTools_DescribesEveryTool()
    {
        // Act
        var undescribed = (await GetTools())
            .Where(t => string.IsNullOrWhiteSpace(t.ProtocolTool.Description))
            .Select(t => t.ProtocolTool.Name)
            .ToList();

        // Assert
        Assert.Empty(undescribed);
    }

    private async Task<IReadOnlyList<ModelContextProtocol.Server.McpServerTool>> GetTools()
    {
        _ = _factory.CreateClient();
        return await _factory.Services.GetRequiredService<McpToolCatalog>().GetTools();
    }

    private static IEnumerable<JsonObject> SnapshotTools() => _snapshot["tools"]!.AsArray().OfType<JsonObject>();

    private static string Hint(bool? value) => value is null ? "null" : JsonSerializer.Serialize(value.Value);
}
