using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.FeatureManagement.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Infrastructure.Auth;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Reads Wayd's OAuth authorization server metadata from the booted host as an MCP client would, against literal
/// member names and paths, and holds it to the protected resource metadata that points clients at it.
/// </summary>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class AuthorizationServerMetadataTests(WaydSqlServerApiFactory factory)
{
    private const string MetadataPath = "/.well-known/oauth-authorization-server";

    private readonly WaydSqlServerApiFactory _factory = factory;

    [Fact]
    public async Task Metadata_DescribesAPublicClientCodeFlowServer_ToAnAnonymousCaller()
    {
        // Arrange
        await SetFlag(true);
        var client = _factory.CreateClient();
        var issuer = _factory.Server.BaseAddress.GetLeftPart(UriPartial.Authority);

        // Act
        var response = await client.GetAsync(MetadataPath, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var metadata = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(issuer, metadata.GetProperty("issuer").GetString());
        Assert.Equal($"{issuer}/oauth/authorize", metadata.GetProperty("authorization_endpoint").GetString());
        Assert.Equal($"{issuer}/oauth/token", metadata.GetProperty("token_endpoint").GetString());
        Assert.Equal(["code"], Strings(metadata, "response_types_supported"));
        Assert.Equal(["authorization_code", "refresh_token"], Strings(metadata, "grant_types_supported"));
        Assert.Equal(["S256"], Strings(metadata, "code_challenge_methods_supported"));
        Assert.Equal(["none"], Strings(metadata, "token_endpoint_auth_methods_supported"));
        Assert.Equal(["wayd:read", "wayd:write"], Strings(metadata, "scopes_supported"));
        Assert.True(metadata.GetProperty("client_id_metadata_document_supported").GetBoolean());
        Assert.True(metadata.GetProperty("authorization_response_iss_parameter_supported").GetBoolean());
    }

    [Fact]
    public async Task Metadata_NamesTheServerTheProtectedResourceMetadataPointsAt()
    {
        // Arrange — a client follows the resource's authorization_servers entry to this document, and must reject it
        // unless its issuer is exactly that entry (RFC 8414 §3.3)
        await SetFlag(true);
        var client = _factory.CreateClient();
        var resource = await client.GetFromJsonAsync<JsonElement>("/.well-known/oauth-protected-resource/mcp", TestContext.Current.CancellationToken);
        var authorizationServer = Assert.Single(Strings(resource, "authorization_servers"));

        // Act
        var metadata = await client.GetFromJsonAsync<JsonElement>(
            new Uri(new Uri(authorizationServer!), MetadataPath).PathAndQuery, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(authorizationServer, metadata.GetProperty("issuer").GetString());
        Assert.Equal(Strings(metadata, "scopes_supported"), Strings(resource, "scopes_supported"));
    }

    [Fact]
    public async Task Metadata_UsesTheConfiguredPublicUrl_NotTheRequestsUrl()
    {
        // Arrange — the API behind a TLS-terminating proxy sees plain HTTP on an internal address
        await SetFlag(true);
        using var behindProxy = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                [new KeyValuePair<string, string?>("ApiSettings:PublicUrl", "https://api.wayd.example.com")])));
        var client = behindProxy.CreateClient();

        // Act
        var metadata = await client.GetFromJsonAsync<JsonElement>(MetadataPath, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("https://api.wayd.example.com", metadata.GetProperty("issuer").GetString());
        Assert.Equal("https://api.wayd.example.com/oauth/authorize", metadata.GetProperty("authorization_endpoint").GetString());
        Assert.Equal("https://api.wayd.example.com/oauth/token", metadata.GetProperty("token_endpoint").GetString());
    }

    [Theory]
    [InlineData(MetadataPath)]
    [InlineData("/oauth/authorize")]
    [InlineData("/oauth/token")]
    public async Task AuthorizationServer_AnswersNotFound_WhileTheFeatureFlagIsOff(string path)
    {
        // Arrange
        await SetFlag(false);
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static string?[] Strings(JsonElement element, string property) =>
        [.. element.GetProperty(property).EnumerateArray().Select(e => e.GetString())];

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
