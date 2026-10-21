using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using NJsonSchema;
using NodaTime;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Authorization;
using Wayd.Infrastructure.Auth;
using Wayd.Common.Application.FeatureManagement.Commands;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Teams.Commands;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Application;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Command;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.Web.Api.IntegrationTests.Infrastructure;
using Wayd.Web.Api.Mcp;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Models;
using Wayd.Organization.Application.Persistence;
using Wayd.Planning.Application.Persistence;
using PlanningInterval = Wayd.Planning.Domain.Models.PlanningInterval;
using PlanningTeam = Wayd.Planning.Domain.Models.PlanningTeam;
using Team = Wayd.Organization.Domain.Models.Team;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Drives <c>/mcp</c> with the MCP SDK's own client, authenticated by a personal access token, and holds
/// each tool call to the answer the matching endpoint gives the same caller. A tool runs through that
/// endpoint, so permission checks, delivery-leadership rules in the domain and validation all apply to it
/// unchanged; these tests are what would notice if that stopped being true.
/// </summary>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class McpEndpointTests(WaydSqlServerApiFactory factory)
{
    private readonly WaydSqlServerApiFactory _factory = factory;

    [Fact]
    public async Task Mcp_AnswersNotFound_WhileTheFeatureFlagIsOff()
    {
        // Arrange
        await SetFlag(false);
        var user = await _factory.CreateAuthenticatedClient();

        // Act
        var response = await user.Client.PostAsync(McpServerSetup.Path, JsonContent.Create(new { }), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Mcp_AnswersNotFound_ToACallerWithoutCredentials_WhileTheFeatureFlagIsOff()
    {
        // Arrange — a challenge would reveal the endpoint and point at metadata that is not served
        await SetFlag(false);
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync(McpServerSetup.Path, JsonContent.Create(new { }), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(response.Headers.WwwAuthenticate);
    }

    [Theory]
    [InlineData("/mcp")]
    [InlineData("/mcp/")]
    [InlineData("/MCP")]
    public async Task Mcp_RefusesACallerWithoutCredentials_PointingAtTheResourceMetadata(string path)
    {
        // Arrange
        await SetFlag(true);
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync(path, JsonContent.Create(new { }), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var challenge = Assert.Single(response.Headers.WwwAuthenticate);
        Assert.Equal("Bearer", challenge.Scheme);
        Assert.Equal(ResourceMetadataParameter(), challenge.Parameter);
    }

    [Fact]
    public async Task Mcp_RefusesAnUnknownPersonalAccessToken_PointingAtTheResourceMetadata()
    {
        // Arrange
        await SetFlag(true);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("x-api-key", "not-a-real-token");

        // Act
        var response = await client.PostAsync(McpServerSetup.Path, JsonContent.Create(new { }), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var challenge = Assert.Single(response.Headers.WwwAuthenticate);
        Assert.Equal("Bearer", challenge.Scheme);
        Assert.Equal(ResourceMetadataParameter(), challenge.Parameter);
    }

    [Fact]
    public async Task Mcp_RefusesAnInvalidBearerToken_AsAnInvalidToken()
    {
        // Arrange
        await SetFlag(true);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "not-a-real-token");

        // Act
        var response = await client.PostAsync(McpServerSetup.Path, JsonContent.Create(new { }), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var challenge = Assert.Single(response.Headers.WwwAuthenticate);
        Assert.Equal("Bearer", challenge.Scheme);
        Assert.Equal($"{ResourceMetadataParameter()}, error=\"invalid_token\"", challenge.Parameter);
    }

    [Fact]
    public async Task Connect_WithAWaydToken_ReceivesTheInstructions()
    {
        // Arrange
        await SetFlag(true);
        var user = await _factory.CreateAuthenticatedClient();
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = new Uri(_factory.Server.BaseAddress, McpServerSetup.Path) },
            user.Client,
            ownsHttpClient: false);

        // Act
        await using var mcp = await McpClient.CreateAsync(transport, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(McpServerInstructions.Text, mcp.ServerInstructions);
    }

    [Fact]
    public async Task ResourceMetadata_NamesTheEndpointAndWaydAsItsAuthorizationServer()
    {
        // Arrange
        await SetFlag(true);
        var client = _factory.CreateClient();
        var origin = _factory.Server.BaseAddress.GetLeftPart(UriPartial.Authority);

        // Act
        var response = await client.GetAsync("/.well-known/oauth-protected-resource/mcp", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var metadata = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal($"{origin}/mcp", metadata.GetProperty("resource").GetString());
        Assert.Equal([origin], metadata.GetProperty("authorization_servers").EnumerateArray().Select(s => s.GetString()));
        Assert.Equal(["header"], metadata.GetProperty("bearer_methods_supported").EnumerateArray().Select(s => s.GetString()));
        Assert.Equal("Wayd", metadata.GetProperty("resource_name").GetString());
    }

    [Fact]
    public async Task ChallengeAndResourceMetadata_UseTheConfiguredPublicUrl_NotTheRequestsUrl()
    {
        // Arrange — the API behind a TLS-terminating proxy sees plain HTTP on an internal address
        await SetFlag(true);
        using var behindProxy = WithPublicUrl("https://api.wayd.example.com/");
        var client = behindProxy.CreateClient();
        using var invalidBearer = new HttpRequestMessage(HttpMethod.Post, McpServerSetup.Path) { Content = JsonContent.Create(new { }) };
        invalidBearer.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "not-a-real-token");

        // Act
        var refused = await client.PostAsync(McpServerSetup.Path, JsonContent.Create(new { }), TestContext.Current.CancellationToken);
        var refusedToken = await client.SendAsync(invalidBearer, TestContext.Current.CancellationToken);
        var metadataResponse = await client.GetAsync("/.well-known/oauth-protected-resource/mcp", TestContext.Current.CancellationToken);

        // Assert
        const string expected = "resource_metadata=\"https://api.wayd.example.com/.well-known/oauth-protected-resource/mcp\"";
        Assert.Equal(expected, Assert.Single(refused.Headers.WwwAuthenticate).Parameter);
        Assert.Equal($"{expected}, error=\"invalid_token\"", Assert.Single(refusedToken.Headers.WwwAuthenticate).Parameter);
        Assert.Equal(HttpStatusCode.OK, metadataResponse.StatusCode);
        var metadata = await metadataResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("https://api.wayd.example.com/mcp", metadata.GetProperty("resource").GetString());
        Assert.Equal(["https://api.wayd.example.com"], metadata.GetProperty("authorization_servers").EnumerateArray().Select(s => s.GetString()));
    }

    [Fact]
    public async Task ResourceMetadata_AnswersNotFound_WhileTheFeatureFlagIsOff()
    {
        // Arrange
        await SetFlag(false);
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/.well-known/oauth-protected-resource/mcp", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/.well-known/oauth-protected-resource")]
    [InlineData("/.well-known/oauth-protected-resource/api/ppm/projects")]
    public async Task ResourceMetadata_AnswersNotFound_ForAnythingButTheEndpoint(string path)
    {
        // Arrange
        await SetFlag(true);
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Connect_WithAPersonalAccessToken_ReceivesTheInstructionsAndEveryTool()
    {
        // Arrange
        await SetFlag(true);
        var token = await CreateToken();

        // Act
        await using var mcp = await Connect(token);
        var tools = await mcp.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(McpServerInstructions.Text, mcp.ServerInstructions);
        var expected = await _factory.Services.GetRequiredService<McpToolCatalog>().GetTools();
        Assert.Equal(expected.Count, tools.Count);
        var getProject = Assert.Single(tools, t => t.Name == "Projects_GetProject");
        Assert.True(getProject.ProtocolTool.Annotations?.ReadOnlyHint);
        Assert.False(getProject.ProtocolTool.Annotations?.OpenWorldHint);
    }

    [Fact]
    public async Task Connect_WithToolsetsAndReadOnly_ReceivesOnlyThoseToolsetsReadOnlyTools()
    {
        // Arrange
        await SetFlag(true);
        var token = await CreateToken();
        var filter = new McpToolFilter(new HashSet<McpToolset> { McpToolset.Planning, McpToolset.Teams }, true);

        // Act
        await using var mcp = await Connect(token, "?toolsets=planning,teams&readonly=true");
        var tools = await mcp.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var expected = await _factory.Services.GetRequiredService<McpToolCatalog>().GetTools(filter);
        Assert.NotEmpty(expected);
        Assert.Equal(expected.Select(t => t.ProtocolTool.Name), tools.Select(t => t.Name).Order(StringComparer.Ordinal));
        Assert.All(tools, t => Assert.True(t.ProtocolTool.Annotations?.ReadOnlyHint));
    }

    [Fact]
    public async Task Connect_ReadsTheToolsetsHeader()
    {
        // Arrange
        await SetFlag(true);
        var token = await CreateToken();
        var filter = new McpToolFilter(new HashSet<McpToolset> { McpToolset.Imports }, false);

        // Act
        await using var mcp = await Connect(token, headers: new() { [McpToolFilter.ToolsetsHeader] = "imports" });
        var tools = await mcp.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var expected = await _factory.Services.GetRequiredService<McpToolCatalog>().GetTools(filter);
        Assert.Equal(expected.Select(t => t.ProtocolTool.Name), tools.Select(t => t.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Prompts_OfferEverySkill_WhateverToolsetsTheConnectionChose()
    {
        // Arrange
        await SetFlag(true);
        var token = await CreateToken();
        await using var mcp = await Connect(token, "?toolsets=teams&readonly=true");

        // Act
        var prompts = await mcp.ListPromptsAsync(cancellationToken: TestContext.Current.CancellationToken);
        var ppm = await mcp.GetPromptAsync("wayd-ppm", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(mcp.ServerCapabilities.Prompts);
        Assert.Equal(SkillPrompts.All.Select(p => p.ProtocolPrompt.Name), prompts.Select(p => p.Name).Order(StringComparer.Ordinal));
        var listed = Assert.Single(prompts, p => p.Name == "wayd-ppm");
        var expected = SkillPrompts.All.Single(p => p.ProtocolPrompt.Name == "wayd-ppm");
        Assert.Equal(expected.ProtocolPrompt.Title, listed.ProtocolPrompt.Title);
        Assert.Equal(expected.ProtocolPrompt.Description, listed.ProtocolPrompt.Description);
        Assert.Equal(
            expected.Result.Messages.Select(m => ((TextContentBlock)m.Content).Text),
            ppm.Messages.Select(m => Assert.IsType<TextContentBlock>(m.Content).Text));
        Assert.All(ppm.Messages, m => Assert.Equal(Role.User, m.Role));
    }

    [Fact]
    public async Task CallTool_IsRefused_WhenTheToolIsOutsideTheChosenToolsets()
    {
        // Arrange
        await SetFlag(true);
        var permission = ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Projects);
        var (_, token) = await CreateUserWithToken(permission);
        await using var mcp = await Connect(token, "?toolsets=planning");

        // Act
        var call = () => mcp.CallToolAsync("Projects_GetStatuses", cancellationToken: TestContext.Current.CancellationToken).AsTask();

        // Assert
        await Assert.ThrowsAsync<McpProtocolException>(call);
    }

    [Fact]
    public async Task CallTool_IsRefused_WhenReadOnlyAndTheToolWrites()
    {
        // Arrange — the caller may update portfolios, so only read-only mode stands in the way
        await SetFlag(true);
        var permission = ApplicationPermission.NameFor(ApplicationAction.Update, ApplicationResource.ProjectPortfolios);
        var (_, token) = await CreateUserWithToken(permission);
        var portfolioId = await CreatePortfolio();
        await using var mcp = await Connect(token, "?readonly=true");

        // Act
        var call = () => mcp.CallToolAsync(
            "Portfolios_Update",
            new Dictionary<string, object?> { ["id"] = portfolioId, ["requestBody"] = new { id = portfolioId, name = "Renamed through MCP" } },
            cancellationToken: TestContext.Current.CancellationToken).AsTask();

        // Assert
        await Assert.ThrowsAsync<McpProtocolException>(call);
    }

    [Fact]
    public async Task Mcp_AnswersBadRequest_ForAnUnknownToolset()
    {
        // Arrange
        await SetFlag(true);
        var user = await _factory.CreateAuthenticatedClient();

        // Act
        var response = await user.Client.PostAsync($"{McpServerSetup.Path}?toolsets=ppm,roadmaps", JsonContent.Create(new { }), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("names 'roadmaps', which is not a toolset", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CallTool_AnswersWithWhatTheEndpointReturns()
    {
        // Arrange
        await SetFlag(true);
        var permission = ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Projects);
        var (user, token) = await CreateUserWithToken(permission);
        await using var mcp = await Connect(token);
        var direct = await user.Client.GetStringAsync("/api/ppm/projects/statuses", TestContext.Current.CancellationToken);

        // Act
        var result = await mcp.CallToolAsync("Projects_GetStatuses", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(true, result.IsError);
        Assert.Equal(direct, TextOf(result));
    }

    [Fact]
    public async Task CallTool_RecordsItsDuration_ByToolAndTheCredentialTheCallerUsed()
    {
        // Arrange — the web app's token and a personal access token, for the same tool
        await SetFlag(true);
        var permission = ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Projects);
        var (user, token) = await CreateUserWithToken(permission);
        await using var withToken = await Connect(token);
        await using var withSession = await McpClient.CreateAsync(
            new HttpClientTransport(
                new HttpClientTransportOptions { Endpoint = new Uri(_factory.Server.BaseAddress, McpServerSetup.Path) },
                user.Client,
                ownsHttpClient: false),
            cancellationToken: TestContext.Current.CancellationToken);

        var recorded = new System.Collections.Concurrent.ConcurrentBag<(string? Tool, string? Credential, int? Status)>();
        using var listener = new System.Diagnostics.Metrics.MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == Wayd.Infrastructure.OpenTelemetry.McpTelemetry.Name
                && instrument.Name == Wayd.Infrastructure.OpenTelemetry.McpTelemetry.ToolCallDuration)
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((_, _, tags, _) =>
        {
            string? tool = null, credential = null;
            int? status = null;
            foreach (var tag in tags)
            {
                if (tag.Key == Wayd.Infrastructure.OpenTelemetry.McpTelemetry.ToolTag) tool = tag.Value as string;
                else if (tag.Key == Wayd.Infrastructure.OpenTelemetry.McpTelemetry.CredentialTag) credential = tag.Value as string;
                else if (tag.Key == "http.response.status_code") status = tag.Value as int?;
            }
            recorded.Add((tool, credential, status));
        });
        listener.Start();

        // Act
        await withSession.CallToolAsync("Projects_GetStatuses", cancellationToken: TestContext.Current.CancellationToken);
        await withToken.CallToolAsync("Projects_GetStatuses", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(("Projects_GetStatuses", "session", 200), recorded);
        Assert.Contains(("Projects_GetStatuses", "personal_access_token", 200), recorded);
    }

    [Fact]
    public async Task CallTool_AnswersWithAnArrayAsStructuredContentUnderResult()
    {
        // Arrange
        await SetFlag(true);
        var permission = ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Projects);
        var (user, token) = await CreateUserWithToken(permission);
        await using var mcp = await Connect(token);
        var direct = JsonNode.Parse(await user.Client.GetStringAsync("/api/ppm/projects/statuses", TestContext.Current.CancellationToken));

        // Act
        var result = await mcp.CallToolAsync("Projects_GetStatuses", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(true, result.IsError);
        Assert.IsType<JsonArray>(direct);
        Assert.True(JsonNode.DeepEquals(direct, StructuredContentOf(result)[ToolOutput.ResultProperty]));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1.0)]
    [InlineData("1")]
    public async Task CallTool_SkipsTheOffsetItself_AndSaysWhichItemsItShows(object offset)
    {
        // Arrange
        await SetFlag(true);
        var permission = ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Projects);
        var (user, token) = await CreateUserWithToken(permission);
        await using var mcp = await Connect(token);
        var direct = JsonNode.Parse(await user.Client.GetStringAsync("/api/ppm/projects/statuses", TestContext.Current.CancellationToken))!.AsArray();

        // Act
        var result = await mcp.CallToolAsync(
            "Projects_GetStatuses",
            new Dictionary<string, object?> { [ResultBounds.OffsetArgument] = offset },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(true, result.IsError);
        Assert.True(JsonNode.DeepEquals(new JsonArray([.. direct.Skip(1).Select(s => s?.DeepClone())]), StructuredContentOf(result)[ToolOutput.ResultProperty]));
        Assert.Equal($"Showing items 2–{direct.Count} of {direct.Count}.", result.Content.OfType<TextContentBlock>().Last().Text);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1.5)]
    [InlineData("first")]
    public async Task CallTool_RefusesAnOffsetThatIsNotAWholeNumber(object offset)
    {
        // Arrange
        await SetFlag(true);
        var permission = ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Projects);
        var (_, token) = await CreateUserWithToken(permission);
        await using var mcp = await Connect(token);

        // Act
        var result = await mcp.CallToolAsync(
            "Projects_GetStatuses",
            new Dictionary<string, object?> { [ResultBounds.OffsetArgument] = offset },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(
            "Invalid arguments for tool 'Projects_GetStatuses': offset must be a whole number of 0 or more.",
            result.Content.OfType<TextContentBlock>().Single().Text);
    }

    [Fact]
    public async Task CallTool_AnswersWithAnObjectAsTheStructuredContentItself()
    {
        // Arrange
        await SetFlag(true);
        var permission = ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.ProjectPortfolios);
        var (user, token) = await CreateUserWithToken(permission);
        var portfolioId = await CreatePortfolio();
        await using var mcp = await Connect(token);
        var direct = JsonNode.Parse(await user.Client.GetStringAsync($"/api/ppm/portfolios/{portfolioId}", TestContext.Current.CancellationToken));

        // Act
        var result = await mcp.CallToolAsync(
            "Portfolios_GetPortfolio",
            new Dictionary<string, object?> { ["idOrKey"] = portfolioId.ToString() },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(true, result.IsError);
        Assert.True(JsonNode.DeepEquals(direct, StructuredContentOf(result)));
    }

    [Fact]
    public async Task CallTool_AnswersWithStructuredContentItsOutputSchemaAccepts()
    {
        // Arrange — clients may reject a result that breaks the tool's schema, so a schema the OpenAPI
        // document gets wrong breaks the tool. Calls every read-only tool that needs no arguments, one that
        // answers with a record and both import tools, as a caller holding every permission, with a team and
        // a portfolio in place so the lists have an item to check.
        await SetFlag(true);
        var (_, token) = await CreateUserWithToken([.. ApplicationPermissions.All.Select(p => p.Name)]);
        var portfolioId = await CreatePortfolio();
        await CreateTeam();
        await using var mcp = await Connect(token);
        var tools = (await mcp.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken)).Select(t => t.ProtocolTool).ToList();
        var calls = tools
            .Where(t => t.Annotations?.ReadOnlyHint == true && !t.InputSchema.TryGetProperty("required", out _))
            .Select(t => (Tool: t, Arguments: (Dictionary<string, object?>?)null))
            .Append((tools.Single(t => t.Name == "Portfolios_GetPortfolio"), new Dictionary<string, object?> { ["idOrKey"] = portfolioId.ToString() }))
            .Append((tools.Single(t => t.Name == "Imports_GetFileFormat"), new Dictionary<string, object?> { ["importType"] = "strategic.themes" }))
            .Append((tools.Single(t => t.Name == "Imports_Preflight"), new Dictionary<string, object?>
            {
                ["importType"] = "strategic.themes",
                ["file"] = $"ImportId,Name,Description,State\ns1,MCP {Guid.NewGuid():N},Checked through MCP.,Active\n",
            }));

        // Act
        var answered = 0;
        var skipped = new List<string>();
        var violations = new List<string>();
        foreach (var (tool, arguments) in calls)
        {
            var result = await mcp.CallToolAsync(tool.Name, arguments, cancellationToken: TestContext.Current.CancellationToken);
            if (result.IsError == true)
            {
                skipped.Add(tool.Name);
                continue;
            }

            answered++;
            if (tool.OutputSchema is not { } outputSchema || result.StructuredContent is not { } structured)
            {
                violations.Add($"{tool.Name}: answered without an output schema and structured content");
                continue;
            }

            var schema = await JsonSchema.FromJsonAsync(outputSchema.GetRawText(), TestContext.Current.CancellationToken);
            violations.AddRange(schema.Validate(structured.GetRawText()).Select(e => $"{tool.Name}: {e}"));
        }

        // Assert
        Assert.Empty(violations);
        Assert.True(answered >= 25, $"only {answered} tools answered, too few to trust the check; refused: {string.Join(", ", skipped)}");
        Assert.DoesNotContain("Imports_GetFileFormat", skipped);
        Assert.DoesNotContain("Imports_Preflight", skipped);
    }

    [Fact]
    public async Task CallTool_AnswersHealthCheckEvidenceItsOutputSchemaAccepts()
    {
        // Arrange — the evidence tool composes several queries' DTOs into one answer, so its schema is
        // checked on its own: it needs a project, which the no-argument sweep above never has.
        await SetFlag(true);
        var permission = ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Projects);
        var (_, token) = await CreateUserWithToken(permission);
        var projectKey = await CreateProject();
        await using var mcp = await Connect(token);
        var tool = (await mcp.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken))
            .Select(t => t.ProtocolTool)
            .Single(t => t.Name == "Projects_GetHealthCheckEvidence");

        // Act
        var result = await mcp.CallToolAsync(
            tool.Name,
            new Dictionary<string, object?> { ["idOrKey"] = projectKey },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(true, result.IsError);
        var structured = StructuredContentOf(result);
        var schema = await JsonSchema.FromJsonAsync(Assert.NotNull(tool.OutputSchema).GetRawText(), TestContext.Current.CancellationToken);
        Assert.Empty(schema.Validate(structured.ToJsonString()));
        Assert.Equal(projectKey, structured["project"]?["key"]?.GetValue<string>());
        Assert.False(string.IsNullOrEmpty(structured["criticalPathUnavailableReason"]?.GetValue<string>()));
        Assert.True(structured["forecast"] is null != structured["forecastUnavailableReason"] is null,
            "a forecast and the reason there is none are exclusive");
    }

    [Fact]
    public async Task CallTool_AnswersObjectiveHealthCheckEvidenceItsOutputSchemaAccepts()
    {
        // Arrange
        await SetFlag(true);
        var (_, token) = await CreateUserWithToken(
            ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.PlanningIntervals),
            ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.PlanningIntervalObjectives));
        var (planningIntervalId, objectiveId) = await CreatePlanningIntervalObjective();
        await using var mcp = await Connect(token);
        var tool = (await mcp.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken))
            .Select(t => t.ProtocolTool)
            .Single(t => t.Name == "PlanningIntervals_GetObjectiveHealthCheckEvidence");

        // Act
        var result = await mcp.CallToolAsync(
            tool.Name,
            new Dictionary<string, object?> { ["idOrKey"] = planningIntervalId.ToString(), ["objectiveIdOrKey"] = objectiveId.ToString() },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(true, result.IsError);
        var structured = StructuredContentOf(result);
        var schema = await JsonSchema.FromJsonAsync(Assert.NotNull(tool.OutputSchema).GetRawText(), TestContext.Current.CancellationToken);
        Assert.Empty(schema.Validate(structured.ToJsonString()));
        Assert.Equal(objectiveId.ToString(), structured["objective"]?["id"]?.GetValue<string>());
        Assert.True(structured["forecast"] is null != structured["forecastUnavailableReason"] is null,
            "a forecast and the reason there is none are exclusive");
    }

    [Fact]
    public async Task CallTool_IsRefused_WhenTheCallerLacksTheEndpointsPermission()
    {
        // Arrange
        await SetFlag(true);
        var (_, token) = await CreateUserWithToken();
        await using var mcp = await Connect(token);

        // Act
        var result = await mcp.CallToolAsync("Projects_GetStatuses", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsError);
        Assert.StartsWith("API Error: Status 403", TextOf(result));
    }

    [Fact]
    public async Task CallTool_IsRefusedAsTheEndpointRefusesIt_WhenTheDomainDeniesTheChange()
    {
        // Arrange — the caller holds the update permission but no delivery-leadership role on the portfolio
        await SetFlag(true);
        var permission = ApplicationPermission.NameFor(ApplicationAction.Update, ApplicationResource.ProjectPortfolios);
        var (user, token) = await CreateUserWithToken(permission);
        var portfolioId = await CreatePortfolio();
        var request = new { id = portfolioId, name = "Renamed through MCP", description = "Must not be applied." };

        var direct = await user.Client.PutAsJsonAsync($"/api/ppm/portfolios/{portfolioId}", request, TestContext.Current.CancellationToken);
        await using var mcp = await Connect(token);

        // Act
        var result = await mcp.CallToolAsync(
            "Portfolios_Update",
            new Dictionary<string, object?> { ["id"] = portfolioId, ["requestBody"] = request },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(direct.IsSuccessStatusCode, "the setup must be one the endpoint refuses");
        Assert.True(result.IsError);
        Assert.StartsWith($"API Error: Status {(int)direct.StatusCode}", TextOf(result));
    }

    [Fact]
    public async Task CallTool_SendsAPatchAsJsonPatch_AndIsAnsweredAsTheEndpointAnswersIt()
    {
        // Arrange — the caller holds the update permission but no delivery-leadership role, so a patch that
        // reaches the command is refused; one sent with the wrong content type would be a 415
        await SetFlag(true);
        var permission = ApplicationPermission.NameFor(ApplicationAction.Update, ApplicationResource.ProjectPortfolios);
        var (user, token) = await CreateUserWithToken(permission);
        var portfolioId = await CreatePortfolio();
        var patch = new[] { new { op = "replace", path = "/name", value = "Renamed through MCP" } };

        using var content = new StringContent(JsonSerializer.Serialize(patch), System.Text.Encoding.UTF8, "application/json-patch+json");
        var direct = await user.Client.PatchAsync($"/api/ppm/portfolios/{portfolioId}", content, TestContext.Current.CancellationToken);
        await using var mcp = await Connect(token);

        // Act
        var result = await mcp.CallToolAsync(
            "Portfolios_Patch",
            new Dictionary<string, object?> { ["id"] = portfolioId, ["requestBody"] = patch },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(direct.IsSuccessStatusCode, "the setup must be one the endpoint refuses");
        Assert.NotEqual(HttpStatusCode.UnsupportedMediaType, direct.StatusCode);
        Assert.True(result.IsError);
        Assert.StartsWith($"API Error: Status {(int)direct.StatusCode}", TextOf(result));
    }

    [Fact]
    public async Task CallTool_WithAPersonalAccessToken_ChangesAsTheUser_ThroughNoClient()
    {
        // Arrange — a personal access token is forwarded as it came, so the change is the user's own
        await SetFlag(true);
        var permission = ApplicationPermission.NameFor(ApplicationAction.Create, ApplicationResource.ProjectPortfolios);
        var (user, token) = await CreateUserWithToken(permission);
        await using var mcp = await Connect(token);

        // Act
        var result = await mcp.CallToolAsync(
            "Portfolios_Create",
            new Dictionary<string, object?> { ["requestBody"] = new { name = $"PAT {Guid.NewGuid():N}"[..20], description = "Created through a tool call." } },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsError is not true, TextOf(result));
        var portfolioId = Guid.Parse(StructuredContentOf(result)["id"]!.GetValue<string>());
        using var scope = _factory.Services.CreateScope();
        var entries = await scope.ServiceProvider.GetRequiredService<Wayd.Infrastructure.Persistence.Context.WaydDbContext>().ActivityLogs
            .AsNoTracking()
            .Where(a => a.AggregateId == portfolioId)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.NotEmpty(entries);
        Assert.All(entries, entry =>
        {
            Assert.Null(entry.ClientId);
            Assert.Equal(EventActorKind.User, entry.ActorKind);
            Assert.Equal(user.UserId, entry.UserId);
        });
    }

    /// <summary>The <c>resource_metadata</c> parameter every challenge from the endpoint carries, written out rather than read from the API's constants.</summary>
    private string ResourceMetadataParameter() =>
        $"resource_metadata=\"{_factory.Server.BaseAddress.GetLeftPart(UriPartial.Authority)}/.well-known/oauth-protected-resource/mcp\"";

    /// <summary>A host over the same database whose <c>ApiSettings:PublicUrl</c> is <paramref name="publicUrl"/>.</summary>
    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> WithPublicUrl(string publicUrl) =>
        _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                [new KeyValuePair<string, string?>("ApiSettings:PublicUrl", publicUrl)])));

    private static string TextOf(CallToolResult result) => Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;

    private static JsonObject StructuredContentOf(CallToolResult result) =>
        Assert.IsType<JsonObject>(JsonNode.Parse(Assert.NotNull(result.StructuredContent).GetRawText()));

    private async Task<McpClient> Connect(string token, string query = "", Dictionary<string, string>? headers = null)
    {
        headers ??= [];
        headers["x-api-key"] = token;
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(_factory.Server.BaseAddress, McpServerSetup.Path + query),
                AdditionalHeaders = headers,
            },
            _factory.CreateClient(),
            ownsHttpClient: true);

        return await McpClient.CreateAsync(transport, cancellationToken: TestContext.Current.CancellationToken);
    }

    private async Task<string> CreateToken() => (await CreateUserWithToken()).Token;

    private async Task<(AuthenticatedClient User, string Token)> CreateUserWithToken(params string[] permissions)
    {
        var user = await _factory.CreateAuthenticatedClient(permissions);
        var response = await user.Client.PostAsJsonAsync(
            "/api/user-management/personal-access-tokens",
            new { name = $"MCP test {Guid.NewGuid():N}"[..24], expiresAt = SystemClock.Instance.GetCurrentInstant().Plus(Duration.FromDays(7)).ToString() },
            TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var created = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return (user, created.GetProperty("token").GetString()!);
    }

    private async Task<Guid> CreatePortfolio()
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>().SetCurrentUserId("integration-test-harness");
        var created = await scope.ServiceProvider.GetRequiredService<IDispatcher>().Send(
            new CreateProjectPortfolioCommand($"MCP {Guid.NewGuid():N}"[..20], "Created by the MCP endpoint tests.", null, null, null),
            TestContext.Current.CancellationToken);
        Assert.True(created.IsSuccess, created.IsFailure ? created.Error : null);
        return created.Value.Id;
    }

    /// <summary>Creates a project in a new, active portfolio and returns its key.</summary>
    private async Task<string> CreateProject()
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>().SetCurrentUserId("integration-test-harness");
        var dbContext = scope.ServiceProvider.GetRequiredService<IProjectPortfolioManagementDbContext>();
        var now = SystemClock.Instance.GetCurrentInstant();

        var category = ExpenditureCategory.Create($"MCP {Guid.NewGuid():N}"[..20], "Created by the MCP endpoint tests.", false, false);
        var portfolio = ProjectPortfolio.Create($"MCP {Guid.NewGuid():N}"[..20], "Created by the MCP endpoint tests.", null, EventActor.System, now);
        dbContext.ExpenditureCategories.Add(category);
        dbContext.Portfolios.Add(portfolio);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        portfolio.Activate(PpmActor.System, now.InUtc().Date, now);
        var key = new ProjectKey($"M{Guid.NewGuid():N}"[..12].ToUpperInvariant());
        var project = portfolio.CreateProject(
            "MCP evidence", "Created by the MCP endpoint tests.", key, category.Id, null, null, null, null, null, null, now, PpmActor.System);
        Assert.True(project.IsSuccess, project.IsFailure ? project.Error : null);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return key.Value;
    }

    /// <summary>
    /// Creates a planning interval spanning today with one team objective and returns both ids. The team's
    /// Planning copy is written directly, with the team's events cleared so replication does not race it.
    /// </summary>
    private async Task<(Guid PlanningIntervalId, Guid ObjectiveId)> CreatePlanningIntervalObjective()
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>().SetCurrentUserId("integration-test-harness");
        var organizationDbContext = scope.ServiceProvider.GetRequiredService<IOrganizationDbContext>();
        var planningDbContext = scope.ServiceProvider.GetRequiredService<IPlanningDbContext>();
        var now = SystemClock.Instance.GetCurrentInstant();
        var today = now.InUtc().Date;

        var code = $"M{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var team = Team.Create($"MCP {code}", new TeamCode(code), null, today.PlusDays(-90),
            Methodology.Scrum, SizingMethod.StoryPoints, "UTC", 1, WorkingWeek.MondayToFriday, EventActor.System, now);
        team.ClearDomainEvents();
        organizationDbContext.Teams.Add(team);
        await organizationDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var planningTeam = new PlanningTeam(team, now);
        planningDbContext.PlanningTeams.Add(planningTeam);
        await planningDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var interval = PlanningInterval.Create($"MCP PI {Guid.NewGuid():N}"[..20], null,
            new LocalDateRange(today.PlusDays(-28), today.PlusDays(28)), 2, "Iteration ", EventActor.System, now);
        Assert.True(interval.IsSuccess, interval.IsFailure ? interval.Error : null);
        interval.Value.ManageTeams([planningTeam.Id], EventActor.System, now);
        var objective = interval.Value.CreateObjective(planningTeam, "MCP evidence", null, false, null, null, 1, EventActor.System, now);
        Assert.True(objective.IsSuccess, objective.IsFailure ? objective.Error : null);
        planningDbContext.PlanningIntervals.Add(interval.Value);
        await planningDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (interval.Value.Id, objective.Value.Id);
    }

    private async Task CreateTeam()
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>().SetCurrentUserId("integration-test-harness");
        var created = await scope.ServiceProvider.GetRequiredService<IDispatcher>().Send(
            new CreateTeamCommand($"MCP {Guid.NewGuid():N}"[..20], new TeamCode($"M{Guid.NewGuid():N}"[..8].ToUpperInvariant()), null, new LocalDate(2026, 1, 1)),
            TestContext.Current.CancellationToken);
        Assert.True(created.IsSuccess, created.IsFailure ? created.Error : null);
    }

    private async Task SetFlag(bool enabled)
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>().SetCurrentUserId("integration-test-harness");
        var flagId = await scope.ServiceProvider.GetRequiredService<IFeatureManagementDbContext>().FeatureFlags
            .Where(f => f.Name == FeatureFlags.Names.McpServer)
            .Select(f => f.Id)
            .SingleAsync(TestContext.Current.CancellationToken);

        var toggled = await scope.ServiceProvider.GetRequiredService<IDispatcher>().Send(
            new ToggleFeatureFlagCommand(flagId, enabled), TestContext.Current.CancellationToken);
        Assert.True(toggled.IsSuccess, toggled.IsFailure ? toggled.Error : null);
    }
}
