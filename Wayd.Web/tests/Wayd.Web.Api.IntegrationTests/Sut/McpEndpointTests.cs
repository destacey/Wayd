using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using NodaTime;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Authorization;
using Wayd.Infrastructure.Auth;
using Wayd.Common.Application.FeatureManagement.Commands;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Command;
using Wayd.Web.Api.IntegrationTests.Infrastructure;
using Wayd.Web.Api.Mcp;

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
    public async Task Mcp_RefusesACallerWithoutCredentials()
    {
        // Arrange
        await SetFlag(true);
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync(McpServerSetup.Path, JsonContent.Create(new { }), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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

    private static string TextOf(CallToolResult result) => Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;

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
