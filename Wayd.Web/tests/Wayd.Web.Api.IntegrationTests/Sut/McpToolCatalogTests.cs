using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NodaTime;
using Wayd.Common.Application.Dtos;
using Wayd.Planning.Application.Roadmaps.Dtos;
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
    public async Task GetTools_OffersEveryToolTheNpmServerOffers()
    {
        // Arrange — the hosted server may offer more: composite tools exist only here
        var expected = SnapshotTools().Select(t => (string)t["name"]!).ToList();

        // Act
        var names = (await GetTools()).Select(t => t.ProtocolTool.Name).ToHashSet(StringComparer.Ordinal);

        // Assert
        Assert.Empty(expected.Where(n => !names.Contains(n)));
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

    [Fact]
    public async Task GetTools_DeclaresAnOutputSchema_ForEveryReadOnlyTool()
    {
        // Act
        var undeclared = (await GetTools())
            .Where(t => t.ProtocolTool.Annotations?.ReadOnlyHint == true && t.ProtocolTool.OutputSchema is null)
            .Select(t => t.ProtocolTool.Name)
            .ToList();

        // Assert
        Assert.Empty(undeclared);
    }

    [Fact]
    public async Task GetTools_DeclaresOnlyObjectOutputSchemas()
    {
        // Act
        var notObjects = (await GetTools())
            .Where(t => t.ProtocolTool.OutputSchema is { } schema
                && !(schema.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String && type.GetString() == "object"))
            .Select(t => t.ProtocolTool.Name)
            .ToList();

        // Assert
        Assert.Empty(notObjects);
    }

    [Fact]
    public async Task GetTools_DescribesEverySubtypeOfAPolymorphicResult()
    {
        // Arrange — the items as the API writes them: an activity holding a milestone, each tagged with its
        // subtype, which the document describes only as their shared base type
        var tools = (await GetTools()).ToDictionary(t => t.ProtocolTool.Name);
        var outputSchema = Assert.NotNull(tools["Roadmaps_GetItems"].ProtocolTool.OutputSchema);
        var roadmapId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        List<RoadmapItemListDto> items =
        [
            new RoadmapActivityListDto
            {
                Id = activityId,
                RoadmapId = roadmapId,
                Name = "Discovery",
                Type = SimpleNavigationDto.Create(1, "Activity"),
                Start = new LocalDate(2026, 1, 5),
                End = new LocalDate(2026, 3, 27),
                Order = 1,
                Children =
                [
                    new RoadmapMilestoneListDto
                    {
                        Id = Guid.NewGuid(),
                        RoadmapId = roadmapId,
                        Name = "Design signed off",
                        Type = SimpleNavigationDto.Create(2, "Milestone"),
                        Parent = new RoadmapActivityNavigationDto { Id = activityId, Name = "Discovery" },
                        Date = new LocalDate(2026, 2, 13),
                    },
                ],
            },
        ];
        var structured = new JsonObject { [ToolOutput.ResultProperty] = JsonSerializer.SerializeToNode(items, SerializerOptions()) };

        // Act
        var errors = await ValidationErrors(outputSchema, structured);

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public async Task GetTools_DescribesAPolymorphicRecordAsTheEndpointWritesIt()
    {
        // Arrange — the endpoint declares the base type, so the record carries its subtype's $type
        var tools = (await GetTools()).ToDictionary(t => t.ProtocolTool.Name);
        var outputSchema = Assert.NotNull(tools["Roadmaps_GetItem"].ProtocolTool.OutputSchema);
        RoadmapItemDetailsDto item = new RoadmapMilestoneDetailsDto
        {
            Id = Guid.NewGuid(),
            RoadmapId = Guid.NewGuid(),
            Name = "Design signed off",
            Type = SimpleNavigationDto.Create(2, "Milestone"),
            Date = new LocalDate(2026, 2, 13),
        };
        var structured = JsonSerializer.SerializeToNode(item, SerializerOptions())!;

        // Act
        var errors = await ValidationErrors(outputSchema, structured);

        // Assert
        Assert.Equal("milestone", (string?)structured["$type"]);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task GetTools_DescribesASubtypeListWithoutTheBaseTypesDiscriminator()
    {
        // Arrange — the endpoint declares the subtype itself, so System.Text.Json writes no $type
        var tools = (await GetTools()).ToDictionary(t => t.ProtocolTool.Name);
        var outputSchema = Assert.NotNull(tools["Roadmaps_GetActivities"].ProtocolTool.OutputSchema);
        IEnumerable<RoadmapActivityListDto> activities =
        [
            new RoadmapActivityListDto
            {
                Id = Guid.NewGuid(),
                RoadmapId = Guid.NewGuid(),
                Name = "Discovery",
                Type = SimpleNavigationDto.Create(1, "Activity"),
                Start = new LocalDate(2026, 1, 5),
                End = new LocalDate(2026, 3, 27),
            },
        ];
        var structured = new JsonObject { [ToolOutput.ResultProperty] = JsonSerializer.SerializeToNode(activities, SerializerOptions()) };

        // Act
        var errors = await ValidationErrors(outputSchema, structured);

        // Assert
        Assert.Null(structured[ToolOutput.ResultProperty]![0]!["$type"]);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task GetToolsets_PutsToolsInEveryToolset()
    {
        // Act
        var tools = await GetCatalog().GetToolsets();

        // Assert
        Assert.Equal(Enum.GetValues<McpToolset>(), tools.Select(t => t.Toolset).Distinct().Order());
    }

    [Fact]
    public async Task GetTools_OffersOnlyTheChosenToolsetsReadOnlyTools()
    {
        // Arrange
        var all = await GetCatalog().GetToolsets();
        var filter = new McpToolFilter(new HashSet<McpToolset> { McpToolset.Ppm }, true);

        // Act
        var tools = await GetCatalog().GetTools(filter);

        // Assert
        var expected = all
            .Where(t => t.Toolset == McpToolset.Ppm && t.Tool.ProtocolTool.Annotations?.ReadOnlyHint == true)
            .Select(t => t.Tool.ProtocolTool.Name);
        Assert.NotEmpty(expected);
        Assert.Equal(expected, tools.Select(t => t.ProtocolTool.Name));
    }

    private McpToolCatalog GetCatalog()
    {
        _ = _factory.CreateClient();
        return _factory.Services.GetRequiredService<McpToolCatalog>();
    }

    private async Task<IReadOnlyList<ModelContextProtocol.Server.McpServerTool>> GetTools()
    {
        _ = _factory.CreateClient();
        return await _factory.Services.GetRequiredService<McpToolCatalog>().GetTools();
    }

    /// <summary>The options the API writes responses with.</summary>
    private JsonSerializerOptions SerializerOptions() =>
        _factory.Services.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;

    private static async Task<IReadOnlyList<string>> ValidationErrors(JsonElement outputSchema, JsonNode structured)
    {
        var schema = await NJsonSchema.JsonSchema.FromJsonAsync(outputSchema.GetRawText(), TestContext.Current.CancellationToken);
        return [.. schema.Validate(structured.ToJsonString()).Select(e => e.ToString())];
    }

    private static IEnumerable<JsonObject> SnapshotTools() => _snapshot["tools"]!.AsArray().OfType<JsonObject>();

    private static string Hint(bool? value) => value is null ? "null" : JsonSerializer.Serialize(value.Value);
}
