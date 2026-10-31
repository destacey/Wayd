using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NodaTime;
using Wayd.Common.Application.Dtos;
using Wayd.Planning.Application.Roadmaps.Dtos;
using Wayd.Infrastructure.Auth.Permissions;
using Wayd.Web.Api.IntegrationTests.Infrastructure;
using Wayd.Web.Api.Mcp;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Checks the hosted MCP server's tools: against the tools published before it (<c>Mcp/published-tools.json</c>,
/// the retired <c>@wayd/mcp</c> package's tool list), against what the user docs, skills and skill evaluation
/// name, and for the rules every tool follows. Published tool names are a contract with skills, prompts and
/// configurations already in use, so the server must offer every one, advertise the same title and
/// annotations, accept every argument callers send, and require nothing they may leave out.
/// </summary>
/// <remarks>
/// Builds the catalogue from the booted host because it is described from the API's runtime OpenAPI
/// document, which only the whole application can produce.
/// </remarks>
[Collection(InMemoryApiTestCollection.Name)]
public sealed class McpToolCatalogTests(WaydApiFactory factory)
{
    private static readonly JsonObject _snapshot = JsonNode.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Mcp", "published-tools.json")))!.AsObject();

    private readonly WaydApiFactory _factory = factory;

    [Fact]
    public void Actions_AreReadOnlyToolsExactlyWhenAReadOnlyTokenMayCallThem()
    {
        // Arrange — a read-only token is offered only read-only tools, and may call an action sent with anything but
        // GET only when the action is marked as changing nothing; the two must agree, or the token is offered a tool
        // the API refuses, or refused one it should be offered
        var actions = _factory.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>().ApiDescriptionGroups.Items
            .SelectMany(g => g.Items)
            .Select(d => (
                Name: d.ActionDescriptor.DisplayName,
                Method: d.HttpMethod ?? "GET",
                Tool: d.ActionDescriptor.EndpointMetadata.OfType<McpToolAttribute>().FirstOrDefault(),
                Marked: d.ActionDescriptor.EndpointMetadata.OfType<ReadOnlyOperationAttribute>().Any()))
            .ToList();

        // Act
        var markedGets = actions.Where(a => a.Marked && HttpMethods.IsGet(a.Method)).Select(a => a.Name).ToList();
        var disagreeing = actions
            .Where(a => a.Tool is not null && !HttpMethods.IsGet(a.Method))
            .Where(a => McpToolAnnotationDefaults.For(a.Method, a.Tool!).ReadOnlyHint != a.Marked)
            .Select(a => a.Name)
            .ToList();

        // Assert
        Assert.Empty(markedGets);
        Assert.Empty(disagreeing);
        Assert.Contains(actions, a => a.Marked);
    }

    [Fact]
    public async Task GetTools_OffersEveryPublishedTool()
    {
        // Arrange — the server may offer more than was published
        var expected = SnapshotTools().Select(t => (string)t["name"]!).ToList();

        // Act
        var names = (await GetTools()).Select(t => t.ProtocolTool.Name).ToHashSet(StringComparer.Ordinal);

        // Assert
        Assert.DoesNotContain(expected, n => !names.Contains(n));
    }

    [Fact]
    public async Task GetTools_AdvertisesThePublishedTitlesAndAnnotations()
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
    public async Task GetTools_AcceptsEveryPublishedArgumentAndRequiresNoMore()
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

            var publishedArguments = expected["arguments"]!.AsArray().Select(a => (string)a!).ToHashSet();
            var publishedRequired = expected["required"]!.AsArray().Select(a => (string)a!).ToHashSet();

            foreach (var missing in publishedArguments.Except(arguments))
                mismatches.Add($"{name}: does not take '{missing}'");
            foreach (var extra in required.Except(publishedRequired))
                mismatches.Add($"{name}: requires '{extra}', which the published tool did not");
        }

        // Assert
        Assert.Empty(mismatches);
    }

    [Fact]
    public void ServerInstructions_SayHowToFetchASkill()
    {
        // Act
        var instructions = McpServerInstructions.Text;

        // Assert
        Assert.Contains(SkillTools.Name, instructions);
    }

    [Fact]
    public async Task GetTools_EndsEveryToolsDescription_WithThePointerToItsSkill()
    {
        // Arrange
        var pointers = Skills.All.Select(s => SkillTools.Pointer(s.Name)).ToList();

        // Act
        var unpointed = (await GetTools())
            .Where(t => t.ProtocolTool.Name != SkillTools.Name)
            .Where(t => !pointers.Any(p => t.ProtocolTool.Description!.EndsWith(" " + p, StringComparison.Ordinal)))
            .Select(t => t.ProtocolTool.Name)
            .ToList();

        // Assert
        Assert.Empty(unpointed);
    }

    [Fact]
    public async Task GetTools_PointsEachToolToTheSkillWhoseDescriptionNamesItsPrefix()
    {
        // Arrange — each skill's description opens by naming the tool prefixes it covers (Projects_*)
        var tools = (await GetTools()).Where(t => t.ProtocolTool.Name != SkillTools.Name);

        // Act
        var mismatches = tools
            .Select(t => (Name: t.ProtocolTool.Name, Skill: Skills.All.Single(s => t.ProtocolTool.Description!.EndsWith(SkillTools.Pointer(s.Name), StringComparison.Ordinal))))
            .Where(t => !t.Skill.Description.Contains(t.Name[..(t.Name.IndexOf('_') + 1)] + "*", StringComparison.Ordinal))
            .Select(t => $"{t.Name} points to {t.Skill.Name}, whose description does not name {t.Name[..(t.Name.IndexOf('_') + 1)]}*")
            .ToList();

        // Assert
        Assert.Empty(mismatches);
    }

    [Fact]
    public async Task GetTools_GivesEveryApiToolTheSkillItsDescriptionNames()
    {
        // Arrange
        var tools = (await GetTools()).OfType<ApiEndpointTool>();

        // Act
        var mismatched = tools
            .Where(t => t.Skill is null || !t.ProtocolTool.Description!.EndsWith(SkillTools.Pointer(t.Skill), StringComparison.Ordinal))
            .Select(t => t.ProtocolTool.Name)
            .ToList();

        // Assert
        Assert.Empty(mismatched);
    }

    [Fact]
    public async Task GetTools_OffersAnOffset_OnlyOnReadToolsThatAnswerWithAnUnpagedList()
    {
        // Act
        var withOffset = (await GetTools())
            .OfType<ApiEndpointTool>()
            .Where(t => t.ProtocolTool.InputSchema.GetProperty("properties").TryGetProperty(ResultBounds.OffsetArgument, out _))
            .Select(t => t.ProtocolTool.Name)
            .ToHashSet(StringComparer.Ordinal);

        // Assert — a list, lists paged by number and by cursor, a list the endpoint limits, a single record, and a write
        Assert.Contains("Projects_GetWorkItems", withOffset);
        Assert.DoesNotContain("Projects_GetActivities", withOffset);
        Assert.DoesNotContain("Me_GetFollows", withOffset);
        Assert.DoesNotContain("Me_GetNotifications", withOffset);
        Assert.DoesNotContain("DeliveryOverview_GetRecentDeliveryEvents", withOffset);
        Assert.DoesNotContain("Projects_GetProject", withOffset);
        Assert.DoesNotContain("Projects_Create", withOffset);
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
    public async Task GetTools_DeclaresAnOutputSchema_ForEveryReadOnlyTool_ButSkillsGet()
    {
        // Act — Skills_Get answers with markdown to read, which an output schema would turn into JSON
        var undeclared = (await GetTools())
            .Where(t => t.ProtocolTool.Name != SkillTools.Name)
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
        Assert.Equal(Enum.GetValues<McpToolset>(), tools.Where(t => t.Toolset is not null).Select(t => t.Toolset!.Value).Distinct().Order());
        Assert.Equal(["Me_Follow", "Me_GetAllocations", "Me_GetFollowCounts", "Me_GetFollowStatus", "Me_GetFollowedRecord", "Me_GetFollows", "Me_GetMutedRecords", "Me_GetNotifications", "Me_GetProfile", "Me_GetReasonMutes", "Me_GetRecordMuteStatus", "Me_GetTeamMemberships", "Me_GetUnreadNotificationCount", "Me_GetWorkItems", "Me_MarkAllNotificationsRead", "Me_MarkNotificationRead", "Me_MarkRecordNotificationsRead", "Me_MuteReason", "Me_MuteRecord", "Me_Unfollow", "Me_UnmuteReason", "Me_UnmuteRecord", SkillTools.Name], tools.Where(t => t.Toolset is null).Select(t => t.Tool.ProtocolTool.Name));
    }

    [Fact]
    public async Task GetTools_OffersOnlyIntakeTools_InTheIntakeToolset()
    {
        // Arrange
        var filter = new McpToolFilter(new HashSet<McpToolset> { McpToolset.Intake }, false);

        // Act
        var tools = (await GetCatalog().GetToolsets())
            .Where(t => t.Toolset == McpToolset.Intake)
            .Select(t => t.Tool.ProtocolTool.Name)
            .ToList();
        var offered = (await GetCatalog().GetTools(filter)).Select(t => t.ProtocolTool.Name).ToList();

        // Assert
        Assert.NotEmpty(tools);
        Assert.All(tools, name => Assert.StartsWith("Intake", name, StringComparison.Ordinal));
        Assert.Contains("IntakeRequests_Submit", offered);
        Assert.Contains("IntakeDesks_Update", offered);
        Assert.DoesNotContain(offered, name => name.StartsWith("Projects_", StringComparison.Ordinal));
        Assert.DoesNotContain(offered, name => name.StartsWith("Discussions_", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetTools_OffersEveryDiscussionTool_InTheDiscussionsToolset()
    {
        // Arrange
        var filter = new McpToolFilter(new HashSet<McpToolset> { McpToolset.Discussions }, false);

        // Act
        var tools = (await GetCatalog().GetToolsets())
            .Where(t => t.Toolset == McpToolset.Discussions)
            .Select(t => t.Tool.ProtocolTool.Name)
            .ToList();
        var offered = (await GetCatalog().GetTools(filter)).Select(t => t.ProtocolTool.Name).ToList();

        // Assert
        // One toolset for the conversation on every kind of record, so a project's needs no intake tools.
        Assert.Equal(
            ["Discussions_Get", "Discussions_GetEntries", "Discussions_GetReplies", "Discussions_GetSettings", "Discussions_Post", "Discussions_Reply",
                "Discussions_SaveSettings"],
            tools.Order(StringComparer.Ordinal));
        Assert.Contains("Discussions_Post", offered);
        Assert.DoesNotContain(offered, name => name.StartsWith("Intake", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetTools_OffersOnlyTheChosenToolsetsReadOnlyTools()
    {
        // Arrange
        var all = await GetCatalog().GetToolsets();
        var filter = new McpToolFilter(new HashSet<McpToolset> { McpToolset.Ppm }, true);

        // Act
        var tools = await GetCatalog().GetTools(filter);

        // Assert — plus Skills_Get, which every connection is offered
        var expected = all
            .Where(t => (t.Toolset == McpToolset.Ppm || t.Toolset is null) && t.Tool.ProtocolTool.Annotations?.ReadOnlyHint == true)
            .Select(t => t.Tool.ProtocolTool.Name);
        Assert.NotEmpty(expected);
        Assert.Contains(SkillTools.Name, expected);
        Assert.Equal(expected, tools.Select(t => t.ProtocolTool.Name));
    }

    [Fact]
    public async Task GetTools_OffersEveryToolTheUserDocsName()
    {
        // Arrange — a row left behind by a renamed or removed tool would send readers to a tool that is gone
        var documented = ToolNamesIn(File.ReadAllText(RepositoryFile("docs", "getting-started", "mcp-server.mdx")));

        // Act
        var names = (await GetTools()).Select(t => t.ProtocolTool.Name).ToHashSet(StringComparer.Ordinal);

        // Assert
        Assert.Empty(documented.Where(n => !names.Contains(n)).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void UserDocs_ListEverySkill_WithTheirCount()
    {
        // Arrange
        var docs = File.ReadAllText(RepositoryFile("docs", "getting-started", "mcp-server.mdx"));
        string[] counts = ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven",
            "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen", "twenty"];

        // Act
        var missing = Skills.All.Select(s => s.Name).Where(n => !docs.Contains($"`/{n}`", StringComparison.Ordinal)).ToList();
        var stated = Regex.Match(docs, @"(\w+) pre-built skills").Groups[1].Value;

        // Assert
        Assert.Empty(missing);
        Assert.Equal(counts[Skills.All.Count], stated);
    }

    [Fact]
    public async Task GetTools_OffersEveryToolTheSkillEvaluationNames()
    {
        // Arrange — the evaluation's stub advertises the tool list fetched from this server, so a scenario
        // naming a tool it lacks could never pass
        var named = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(RepositoryFile("evals", "scenarios"), "*.json"))
        {
            foreach (var scenario in JsonNode.Parse(File.ReadAllText(file))!.AsArray())
            {
                foreach (var fixture in scenario!["fixtures"]?.AsArray() ?? [])
                {
                    named.Add((string)fixture!["tool"]!);
                    if (fixture["after"] is { } after)
                        named.Add((string)after!);
                }
                foreach (var assertion in scenario["expect"]?.AsArray() ?? [])
                {
                    foreach (var key in new[] { "tool", "first", "then" })
                    {
                        if (assertion![key] is { } tool)
                            named.Add((string)tool!);
                    }
                }
            }
        }

        // Act
        var names = (await GetTools()).Select(t => t.ProtocolTool.Name).ToHashSet(StringComparer.Ordinal);

        // Assert
        Assert.NotEmpty(named);
        Assert.Empty(named.Where(n => !names.Contains(n)));
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

    /// <summary>
    /// Every tool name <paramref name="text"/> mentions, matched whole: many names are a prefix of another
    /// (<c>Products_GetProduct</c> of <c>Products_GetProducts</c>), so a substring search would find a removed one.
    /// </summary>
    private static IReadOnlySet<string> ToolNamesIn(string text) =>
        Regex.Matches(text, @"\b[A-Z][A-Za-z]*_[A-Za-z][A-Za-z0-9]*\b").Select(m => m.Value).ToHashSet(StringComparer.Ordinal);

    /// <summary>A path in the repository the tests were built from, found by walking up to <c>Wayd.slnx</c>.</summary>
    private static string RepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Wayd.slnx")))
            directory = directory.Parent;
        return Path.Combine([directory?.FullName ?? throw new InvalidOperationException("Wayd.slnx not found above the test output."), .. parts]);
    }

    private static IEnumerable<JsonObject> SnapshotTools() => _snapshot["tools"]!.AsArray().OfType<JsonObject>();

    private static string Hint(bool? value) => value is null ? "null" : JsonSerializer.Serialize(value.Value);
}
