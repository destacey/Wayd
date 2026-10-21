using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using NodaTime;
using Wayd.Common.Domain.Activities;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.FeatureManagement.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Infrastructure;
using Wayd.Infrastructure.Auth;
using Wayd.Infrastructure.Auth.OAuth;
using Wayd.Infrastructure.Identity;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Web.Api.IntegrationTests.Infrastructure;
using Wayd.Web.Api.Mcp;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Drives Wayd's OAuth authorization server as an MCP client and a user would: the authorization endpoint, the consent
/// API the web app calls, and the token endpoint, then <c>/mcp</c> with the token. Clients are registered with
/// <see cref="FakeOAuthClients"/> rather than fetched.
/// </summary>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class OAuthAuthorizationServerTests(WaydSqlServerApiFactory factory)
{
    private const string RedirectUri = "https://client.acme.example/callback";
    private const string LoopbackRedirectUri = "http://127.0.0.1:33418/callback";
    private const string State = "state-from-the-client";

    // RFC 7636 Appendix B.
    private const string Verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
    private const string Challenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

    private const string ConsentPage = "http://localhost:3000/oauth/consent";

    private static readonly string CreatePortfolios =
        ApplicationPermission.NameFor(ApplicationAction.Create, ApplicationResource.ProjectPortfolios);

    private readonly WaydSqlServerApiFactory _factory = factory;

    private string Issuer => _factory.Server.BaseAddress.GetLeftPart(UriPartial.Authority);

    private string Resource => Issuer + McpServerSetup.Path;

    [Fact]
    public async Task CodeFlow_IssuesATokenBoundToTheMcpResource_ThatMcpAccepts()
    {
        // Arrange
        await SetFlag(true);
        var client = RegisterClient();
        var (user, code) = await Approve(client);

        // Act
        var response = await PostToken(new()
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = RedirectUri,
            ["client_id"] = client.ClientId,
            ["code_verifier"] = Verifier,
            ["resource"] = Resource,
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var tokens = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("Bearer", tokens.GetProperty("token_type").GetString());
        Assert.True(tokens.GetProperty("expires_in").GetInt32() > 0);
        Assert.False(string.IsNullOrEmpty(tokens.GetProperty("refresh_token").GetString()));

        var accessToken = tokens.GetProperty("access_token").GetString()!;
        var jwt = new JsonWebToken(accessToken);
        Assert.Equal(OAuthAccessTokens.TokenType, jwt.Typ);
        Assert.Equal(Issuer, jwt.Issuer);
        Assert.Equal([Resource], jwt.Audiences);
        Assert.Equal(user.UserId, jwt.Subject);
        Assert.Equal(client.ClientId, jwt.GetClaim(OAuthAccessTokens.ClientIdClaim).Value);

        await using var mcp = await ConnectToMcp(accessToken);
        Assert.Equal(McpServerInstructions.Text, mcp.ServerInstructions);
    }

    [Fact]
    public async Task AccessToken_IsRefusedByTheRestOfTheApi()
    {
        // Arrange — the token is for /mcp alone; the API's own endpoints accept only the web app's tokens and PATs
        await SetFlag(true);
        var tokens = await Redeem(RegisterClient());
        var api = _factory.CreateClient();
        api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GetProperty("access_token").GetString());

        // Act
        var response = await api.PostAsJsonAsync("/api/auth/sessions", new { }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CallTool_WithAnAccessToken_ChangesAsTheUser_AndTheActivityLogNamesTheClient()
    {
        // Arrange
        await SetFlag(true);
        var client = RegisterClient();
        var (user, accessToken) = await ConnectedUser(client, CreatePortfolios);
        await using var mcp = await ConnectToMcp(accessToken);

        // Act
        var result = await mcp.CallToolAsync(
            "Portfolios_Create",
            new Dictionary<string, object?> { ["requestBody"] = new { name = $"OAuth {Guid.NewGuid():N}"[..20], description = "Created through a tool call." } },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsError is not true, TextOf(result));
        var portfolioId = Guid.Parse(result.StructuredContent!.Value.GetProperty("id").GetString()!);
        var entries = await ActivityFor(portfolioId);
        Assert.NotEmpty(entries);
        Assert.All(entries, entry =>
        {
            Assert.Equal(client.ClientId, entry.ClientId);
            Assert.Equal(EventActorKind.User, entry.ActorKind);
            Assert.Equal(user.UserId, entry.UserId);
        });
    }

    [Fact]
    public async Task CallTool_WithAnAccessToken_IsRefused_OnceTheUserIsDeactivated()
    {
        // Arrange — the /mcp scheme checks only the session, so the delegated token's own check is what refuses
        await SetFlag(true);
        var (user, accessToken) = await ConnectedUser(RegisterClient(), CreatePortfolios);
        await using var mcp = await ConnectToMcp(accessToken);
        await SetUserActive(user.UserId, false);

        // Act
        var result = await mcp.CallToolAsync(
            "Portfolios_Create",
            new Dictionary<string, object?> { ["requestBody"] = new { name = $"OAuth {Guid.NewGuid():N}"[..20], description = "Created through a tool call." } },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsError);
        Assert.StartsWith("API Error: Status 401", TextOf(result));
    }

    [Theory]
    [InlineData("/api/user-management/personal-access-tokens")]
    [InlineData("/api/oauth/authorization-requests/any-handle")]
    public async Task DelegatedToken_IsRefusedOverHttp(string path)
    {
        // Arrange — a correctly signed delegated token, as if copied out of the process
        await SetFlag(true);
        var client = RegisterClient();
        var (user, _) = await ConnectedUser(client);
        var token = DelegatedAccessTokens.Issue(
            _factory.Services.GetRequiredService<IConfiguration>(), Issuer, user.UserId, client.ClientId, SystemClock.Instance.GetCurrentInstant());
        var api = _factory.CreateClient();
        api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await api.GetAsync(path, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Mcp_RefusesADelegatedToken()
    {
        // Arrange — typed like an access token, but for the API's audience rather than /mcp
        await SetFlag(true);
        var client = RegisterClient();
        var (user, _) = await ConnectedUser(client);
        var token = DelegatedAccessTokens.Issue(
            _factory.Services.GetRequiredService<IConfiguration>(), Issuer, user.UserId, client.ClientId, SystemClock.Instance.GetCurrentInstant());
        var mcp = _factory.CreateClient();
        mcp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await mcp.PostAsJsonAsync(McpServerSetup.Path, new { }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("error=\"invalid_token\"", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task Mcp_RefusesAnAccessTokenForAnotherIssuer()
    {
        // Arrange — the same signing key, but the token says another server issued it
        await SetFlag(true);
        var tokens = await Redeem(RegisterClient());
        using var elsewhere = _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection([new KeyValuePair<string, string?>("ApiSettings:PublicUrl", "https://api.wayd.example.com")])));
        var client = elsewhere.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GetProperty("access_token").GetString());

        // Act
        var response = await client.PostAsJsonAsync(McpServerSetup.Path, new { }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("error=\"invalid_token\"", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task Authorize_SendsTheUserToTheConsentPage_WithAHandleRatherThanTheParameters()
    {
        // Arrange
        await SetFlag(true);
        var client = RegisterClient();

        // Act
        var response = await Browser().GetAsync(AuthorizeUrl(client), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.Equal(ConsentPage, location.GetLeftPart(UriPartial.Path));
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal(["request"], query.Keys);
        Assert.DoesNotContain(State, location.ToString());
    }

    [Theory]
    [InlineData("unknown-client")]
    [InlineData("unregistered-redirect")]
    [InlineData("no-client-id")]
    [InlineData("repeated-redirect")]
    public async Task Authorize_ShowsTheErrorToTheUser_WhenTheClientOrRedirectCannotBeTrusted(string fault)
    {
        // Arrange — RFC 6749 §4.1.2.1: the redirect target is what is in doubt, so nothing is sent to it
        await SetFlag(true);
        var client = RegisterClient();
        var url = fault switch
        {
            "unknown-client" => AuthorizeUrl(client, p => p["client_id"] = "https://unknown.acme.example/metadata.json"),
            "unregistered-redirect" => AuthorizeUrl(client, p => p["redirect_uri"] = "https://attacker.example/callback"),
            "no-client-id" => AuthorizeUrl(client, p => p.Remove("client_id")),
            _ => AuthorizeUrl(client) + "&redirect_uri=" + Uri.EscapeDataString(RedirectUri),
        };

        // Act
        var response = await Browser().GetAsync(url, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("default-src 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task Authorize_EncodesTheReasonItShows()
    {
        // Arrange
        await SetFlag(true);
        var client = RegisterClient();

        // Act
        var response = await Browser().GetAsync(
            AuthorizeUrl(client, p => p["client_id"] = "https://unknown.acme.example/<script>.json"),
            TestContext.Current.CancellationToken);

        // Assert
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains(WebUtility.HtmlEncode(FakeOAuthClients.UnknownClient), html);
        Assert.DoesNotContain("<script>", html);
    }

    [Theory]
    [InlineData("code_challenge", null)]
    [InlineData("code_challenge_method", "plain")]
    [InlineData("code_challenge", "too-short")]
    [InlineData("response_type", "token")]
    [InlineData("response_type", null)]
    [InlineData("state", null)]
    [InlineData("resource", "https://elsewhere.acme.example/mcp")]
    public async Task Authorize_ShowsAProtocolError_RatherThanRedirectingIt(string parameter, string? value)
    {
        // Arrange — anyone can be a client with any redirect URI, so an automatic error redirect would make Wayd's
        // domain an open redirector (RFC 9700 §4.11.2)
        await SetFlag(true);
        var client = RegisterClient();
        var url = AuthorizeUrl(client, p =>
        {
            if (value is null)
                p.Remove(parameter);
            else
                p[parameter] = value;
        });

        // Act
        var response = await Browser().GetAsync(url, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Authorize_DoesNotFetchTheClient_ForARequestThatFailsACheaperCheck()
    {
        // Arrange — the client's document is at a URL the requester chose, so a malformed request must not reach it
        await SetFlag(true);
        var client = RegisterClient();
        var before = _factory.OAuthClients.ResolveCount(client.ClientId);

        // Act
        await Browser().GetAsync(AuthorizeUrl(client, p => p["code_challenge_method"] = "plain"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(before, _factory.OAuthClients.ResolveCount(client.ClientId));
    }

    [Fact]
    public async Task Authorize_AnswersTooManyRequests_PastItsLimit()
    {
        // Arrange — a host of its own, so its limiter starts empty and the lowered limit touches no other test
        await SetFlag(true);
        using var limited = _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(
            [
                new KeyValuePair<string, string?>("RateLimitSettings:OAuthAuthorize:PermitLimit", "2"),
                new KeyValuePair<string, string?>("RateLimitSettings:OAuthAuthorize:WindowSeconds", "3600"),
            ])));
        var browser = limited.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var url = AuthorizeUrl(RegisterClient());
        await browser.GetAsync(url, TestContext.Current.CancellationToken);
        await browser.GetAsync(url, TestContext.Current.CancellationToken);

        // Act
        var response = await browser.GetAsync(url, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.NotNull(response.Headers.RetryAfter);
    }

    [Fact]
    public async Task Consent_DescribesTheRequestToTheSignedInUser()
    {
        // Arrange
        await SetFlag(true);
        var client = RegisterClient();
        var handle = await StartAuthorization(client, LoopbackRedirectUri);
        var user = await _factory.CreateAuthenticatedClient();

        // Act
        var request = await user.Client.GetFromJsonAsync<JsonElement>(
            $"/api/oauth/authorization-requests/{handle}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(client.ClientId, request.GetProperty("clientId").GetString());
        Assert.Equal("client.acme.example", request.GetProperty("clientHost").GetString());
        Assert.Equal("Acme Agent", request.GetProperty("clientName").GetString());
        Assert.Equal(LoopbackRedirectUri, request.GetProperty("redirectUri").GetString());
        Assert.Equal("127.0.0.1", request.GetProperty("redirectHost").GetString());
        Assert.False(request.GetProperty("redirectHostDiffers").GetBoolean(), "a loopback redirect stays on the user's machine");
        Assert.True(request.GetProperty("redirectIsLoopback").GetBoolean());
    }

    [Fact]
    public async Task Consent_ShowsTheClientsNameWithoutCharactersThatDisguiseIt()
    {
        // Arrange
        await SetFlag(true);
        var rightToLeftOverride = ((char)0x202E).ToString();
        var client = RegisterClient(name: "Acme" + rightToLeftOverride + "tnegA");
        var handle = await StartAuthorization(client);
        var user = await _factory.CreateAuthenticatedClient();

        // Act
        var request = await user.Client.GetFromJsonAsync<JsonElement>(
            $"/api/oauth/authorization-requests/{handle}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("AcmetnegA", request.GetProperty("clientName").GetString());
        Assert.False(request.GetProperty("redirectHostDiffers").GetBoolean());
    }

    [Fact]
    public async Task Consent_RequiresASignedInUser()
    {
        // Arrange
        await SetFlag(true);
        var handle = await StartAuthorization(RegisterClient());

        // Act
        var response = await _factory.CreateClient().PostAsync(
            $"/api/oauth/authorization-requests/{handle}/approve", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Consent_AnswersNotFound_WhileTheFeatureFlagIsOff()
    {
        // Arrange
        await SetFlag(true);
        var handle = await StartAuthorization(RegisterClient());
        var user = await _factory.CreateAuthenticatedClient();
        await SetFlag(false);

        // Act
        var response = await user.Client.GetAsync($"/api/oauth/authorization-requests/{handle}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Approve_RedirectsWithCodeStateAndIssuer_AndCannotBeRepeated()
    {
        // Arrange
        await SetFlag(true);
        var handle = await StartAuthorization(RegisterClient());
        var user = await _factory.CreateAuthenticatedClient();

        // Act
        var approved = await user.Client.PostAsync($"/api/oauth/authorization-requests/{handle}/approve", null, TestContext.Current.CancellationToken);
        var again = await user.Client.PostAsync($"/api/oauth/authorization-requests/{handle}/approve", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var redirect = new Uri((await approved.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("redirectUrl").GetString()!);
        Assert.Equal(RedirectUri, redirect.GetLeftPart(UriPartial.Path));
        var query = QueryHelpers.ParseQuery(redirect.Query);
        Assert.False(string.IsNullOrEmpty(query["code"].ToString()));
        Assert.Equal(State, query["state"].ToString());
        Assert.Equal(Issuer, query["iss"].ToString());
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    [Fact]
    public async Task Deny_RedirectsAccessDeniedWithStateAndIssuer()
    {
        // Arrange
        await SetFlag(true);
        var handle = await StartAuthorization(RegisterClient());
        var user = await _factory.CreateAuthenticatedClient();

        // Act
        var denied = await user.Client.PostAsync($"/api/oauth/authorization-requests/{handle}/deny", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, denied.StatusCode);
        var redirect = new Uri((await denied.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("redirectUrl").GetString()!);
        var query = QueryHelpers.ParseQuery(redirect.Query);
        Assert.Equal("access_denied", query["error"].ToString());
        Assert.Equal(State, query["state"].ToString());
        Assert.Equal(Issuer, query["iss"].ToString());
        Assert.False(query.ContainsKey("code"));
    }

    [Fact]
    public async Task Token_RefusesACodeTheSecondTime_AndEndsTheSessionTheFirstOpened()
    {
        // Arrange
        await SetFlag(true);
        var client = RegisterClient();
        var (_, code) = await Approve(client);
        var first = await (await RedeemRequest(client, code)).Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        // Act
        var second = await RedeemRequest(client, code);

        // Assert
        await AssertRefused(second, "invalid_grant");
        await AssertRefused(await RefreshRequest(client, first.GetProperty("refresh_token").GetString()!), "invalid_grant");
    }

    [Fact]
    public async Task Token_SpendsTheCode_EvenWhenTheVerifierIsWrong()
    {
        // Arrange — otherwise a stolen code could be tried against verifier after verifier
        await SetFlag(true);
        var client = RegisterClient();
        var (_, code) = await Approve(client);
        var wrong = await RedeemRequest(client, code, verifier: new string('a', 43));

        // Act
        var right = await RedeemRequest(client, code);

        // Assert
        await AssertRefused(wrong, "invalid_grant");
        await AssertRefused(right, "invalid_grant");
    }

    [Theory]
    [InlineData("client_id")]
    [InlineData("redirect_uri")]
    public async Task Token_RefusesACode_PresentedWithAnotherClientOrRedirect(string parameter)
    {
        // Arrange
        await SetFlag(true);
        var client = RegisterClient();
        var (_, code) = await Approve(client);
        var form = RedeemForm(client, code);
        form[parameter] = parameter == "client_id" ? RegisterClient().ClientId : LoopbackRedirectUri;

        // Act
        var response = await PostToken(form);

        // Assert
        await AssertRefused(response, "invalid_grant");
    }

    [Fact]
    public async Task Token_RefusesAnotherResource()
    {
        // Arrange
        await SetFlag(true);
        var client = RegisterClient();
        var (_, code) = await Approve(client);
        var form = RedeemForm(client, code);
        form["resource"] = "https://elsewhere.acme.example/mcp";

        // Act
        var response = await PostToken(form);

        // Assert
        await AssertRefused(response, "invalid_target");
    }

    [Fact]
    public async Task Refresh_RotatesTheToken_AndEndsTheSessionWhenASupersededOneIsReplayed()
    {
        // Arrange
        await SetFlag(true);
        var client = RegisterClient();
        var issued = await Redeem(client);
        var original = issued.GetProperty("refresh_token").GetString()!;
        var refreshed = await RefreshRequest(client, original);
        var rotated = await refreshed.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var replacement = rotated.GetProperty("refresh_token").GetString()!;

        // Act
        var replay = await RefreshRequest(client, original);

        // Assert
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.NotEqual(original, replacement);
        Assert.False(string.IsNullOrEmpty(rotated.GetProperty("access_token").GetString()));
        await AssertRefused(replay, "invalid_grant");
        await AssertRefused(await RefreshRequest(client, replacement), "invalid_grant");
    }

    [Fact]
    public async Task Refresh_RefusesAnotherClientsToken_WithoutSpendingIt()
    {
        // Arrange
        await SetFlag(true);
        var client = RegisterClient();
        var refreshToken = (await Redeem(client)).GetProperty("refresh_token").GetString()!;

        // Act
        var stolen = await RefreshRequest(RegisterClient(), refreshToken);

        // Assert
        await AssertRefused(stolen, "invalid_grant");
        Assert.Equal(HttpStatusCode.OK, (await RefreshRequest(client, refreshToken)).StatusCode);
    }

    [Fact]
    public async Task Sessions_ListTheConnectedClient_SoTheUserCanRevokeIt()
    {
        // Arrange
        await SetFlag(true);
        var client = RegisterClient();
        var (user, code) = await Approve(client);
        var refreshToken = (await (await RedeemRequest(client, code)).Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("refresh_token").GetString()!;

        // Act
        var sessions = await (await user.Client.PostAsJsonAsync("/api/auth/sessions", new { }, TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var connector = sessions.EnumerateArray().Single(s => s.GetProperty("clientId").GetString() == client.ClientId);
        var revoked = await user.Client.DeleteAsync($"/api/auth/sessions/{connector.GetProperty("id").GetGuid()}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Acme Agent", connector.GetProperty("deviceLabel").GetString());
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        await AssertRefused(await RefreshRequest(client, refreshToken), "invalid_grant");
    }

    [Theory]
    [InlineData("password", "unsupported_grant_type")]
    [InlineData("", "invalid_request")]
    public async Task Token_RefusesOtherGrants(string grantType, string error)
    {
        // Arrange
        await SetFlag(true);

        // Act
        var response = await PostToken(new() { ["grant_type"] = grantType, ["client_id"] = RegisterClient().ClientId });

        // Assert
        await AssertRefused(response, error);
    }

    [Fact]
    public async Task Token_RefusesABodyThatIsNotFormEncoded()
    {
        // Arrange
        await SetFlag(true);

        // Act
        var response = await _factory.CreateClient().PostAsJsonAsync(
            AuthorizationServerMetadata.TokenPath, new { grant_type = "authorization_code" }, TestContext.Current.CancellationToken);

        // Assert
        await AssertRefused(response, "invalid_request");
    }

    private OAuthClient RegisterClient(string name = "Acme Agent") => _factory.OAuthClients.Register(new OAuthClient(
        $"https://client.acme.example/{Guid.NewGuid():N}/client-metadata.json",
        name,
        [RedirectUri, LoopbackRedirectUri],
        null,
        null));

    private HttpClient Browser() => _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private string AuthorizeUrl(OAuthClient client, Action<Dictionary<string, string?>>? change = null)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = client.ClientId,
            ["redirect_uri"] = RedirectUri,
            ["code_challenge"] = Challenge,
            ["code_challenge_method"] = "S256",
            ["state"] = State,
            ["resource"] = Resource,
        };
        change?.Invoke(parameters);
        return QueryHelpers.AddQueryString(AuthorizationServerMetadata.AuthorizePath, parameters);
    }

    /// <summary>Sends the browser to the authorization endpoint and answers the handle it carries to the consent page.</summary>
    private async Task<string> StartAuthorization(OAuthClient client, string redirectUri = RedirectUri)
    {
        var response = await Browser().GetAsync(AuthorizeUrl(client, p => p["redirect_uri"] = redirectUri), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return QueryHelpers.ParseQuery(response.Headers.Location!.Query)["request"].ToString();
    }

    /// <summary>
    /// Has a new user holding <paramref name="permissions"/> approve a new request from <paramref name="client"/>, and
    /// answers the code it was given.
    /// </summary>
    private async Task<(AuthenticatedClient User, string Code)> Approve(OAuthClient client, params string[] permissions)
    {
        var handle = await StartAuthorization(client);
        var user = await _factory.CreateAuthenticatedClient(permissions);
        var approved = await user.Client.PostAsync($"/api/oauth/authorization-requests/{handle}/approve", null, TestContext.Current.CancellationToken);
        approved.EnsureSuccessStatusCode();
        var redirectUrl = (await approved.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("redirectUrl").GetString()!;
        return (user, QueryHelpers.ParseQuery(new Uri(redirectUrl).Query)["code"].ToString());
    }

    /// <summary>Has a new user holding <paramref name="permissions"/> connect <paramref name="client"/>, and answers its access token.</summary>
    private async Task<(AuthenticatedClient User, string AccessToken)> ConnectedUser(OAuthClient client, params string[] permissions)
    {
        var (user, code) = await Approve(client, permissions);
        var response = await RedeemRequest(client, code);
        response.EnsureSuccessStatusCode();
        var tokens = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return (user, tokens.GetProperty("access_token").GetString()!);
    }

    private async Task<List<ActivityLogEntry>> ActivityFor(Guid aggregateId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<WaydDbContext>().ActivityLogs
            .AsNoTracking()
            .Where(a => a.AggregateId == aggregateId)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private static string TextOf(CallToolResult result) => string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text));

    private async Task<JsonElement> Redeem(OAuthClient client)
    {
        var (_, code) = await Approve(client);
        var response = await RedeemRequest(client, code);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    private static Dictionary<string, string> RedeemForm(OAuthClient client, string code, string verifier = Verifier) => new()
    {
        ["grant_type"] = "authorization_code",
        ["code"] = code,
        ["redirect_uri"] = RedirectUri,
        ["client_id"] = client.ClientId,
        ["code_verifier"] = verifier,
    };

    private Task<HttpResponseMessage> RedeemRequest(OAuthClient client, string code, string verifier = Verifier) =>
        PostToken(RedeemForm(client, code, verifier));

    private Task<HttpResponseMessage> RefreshRequest(OAuthClient client, string refreshToken) => PostToken(new()
    {
        ["grant_type"] = "refresh_token",
        ["refresh_token"] = refreshToken,
        ["client_id"] = client.ClientId,
    });

    private Task<HttpResponseMessage> PostToken(Dictionary<string, string> form) =>
        _factory.CreateClient().PostAsync(AuthorizationServerMetadata.TokenPath, new FormUrlEncodedContent(form), TestContext.Current.CancellationToken);

    private static async Task AssertRefused(HttpResponseMessage response, string error)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(error, body.GetProperty("error").GetString());
        Assert.False(body.TryGetProperty("access_token", out _));
    }

    private async Task<McpClient> ConnectToMcp(string accessToken)
    {
        var http = _factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = new Uri(_factory.Server.BaseAddress, McpServerSetup.Path) },
            http,
            ownsHttpClient: true);
        return await McpClient.CreateAsync(transport, cancellationToken: TestContext.Current.CancellationToken);
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

    [Fact]
    public async Task Mcp_RefusesAnAccessToken_OnceTheUserRevokesItsSession()
    {
        // Arrange
        await SetFlag(true);
        var client = RegisterClient();
        var (user, code) = await Approve(client);
        var tokens = await (await RedeemRequest(client, code)).Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var sessionId = new JsonWebToken(tokens.GetProperty("access_token").GetString()!).GetClaim(OAuthAccessTokens.SessionIdClaim).Value;
        var revoked = await user.Client.DeleteAsync($"/api/auth/sessions/{Guid.ParseExact(sessionId, "N")}", TestContext.Current.CancellationToken);
        var mcp = _factory.CreateClient();
        mcp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GetProperty("access_token").GetString());

        // Act
        var response = await mcp.PostAsJsonAsync(McpServerSetup.Path, new { }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("error=\"invalid_token\"", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task Consent_RefusesAPersonalAccessToken()
    {
        // Arrange — approving with a PAT would turn it into a connector session that outlives it
        await SetFlag(true);
        var handle = await StartAuthorization(RegisterClient());
        var user = await _factory.CreateAuthenticatedClient();
        var created = await user.Client.PostAsJsonAsync("/api/user-management/personal-access-tokens",
            new { name = "consent-test", expiresAt = SystemClock.Instance.GetCurrentInstant().Plus(Duration.FromDays(7)).ToString() },
            TestContext.Current.CancellationToken);
        Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var pat = (await created.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("token").GetString();
        var withPat = _factory.CreateClient();
        withPat.DefaultRequestHeaders.Add(AuthConstants.ApiKeyHeaderName, pat);

        // Act
        var approve = await withPat.PostAsync($"/api/oauth/authorization-requests/{handle}/approve", null, TestContext.Current.CancellationToken);
        var read = await withPat.GetAsync($"/api/oauth/authorization-requests/{handle}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, approve.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        var stillPending = await user.Client.GetAsync($"/api/oauth/authorization-requests/{handle}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, stillPending.StatusCode);
    }

    [Fact]
    public async Task Consent_DoesNotFlagAnAppSchemeRedirect_AndNamesNoHostForIt()
    {
        // Arrange — a desktop app's own scheme opens the app on the user's machine
        await SetFlag(true);
        var appRedirect = "cursor://anysphere.cursor-retrieval/oauth/callback";
        var client = _factory.OAuthClients.Register(new OAuthClient(
            $"https://client.acme.example/{Guid.NewGuid():N}/client-metadata.json", "Acme Agent", [appRedirect], null, null));
        var handle = await StartAuthorization(client, appRedirect);
        var user = await _factory.CreateAuthenticatedClient();

        // Act
        var request = await user.Client.GetFromJsonAsync<JsonElement>(
            $"/api/oauth/authorization-requests/{handle}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(JsonValueKind.Null, request.GetProperty("redirectHost").ValueKind);
        Assert.False(request.GetProperty("redirectHostDiffers").GetBoolean());
        Assert.False(request.GetProperty("redirectIsLoopback").GetBoolean());
    }

    [Fact]
    public async Task Consent_FlagsAWebRedirectOnAnotherHost()
    {
        // Arrange
        await SetFlag(true);
        var elsewhere = "https://elsewhere.acme.example/callback";
        var client = _factory.OAuthClients.Register(new OAuthClient(
            $"https://client.acme.example/{Guid.NewGuid():N}/client-metadata.json", "Acme Agent", [elsewhere], null, null));
        var handle = await StartAuthorization(client, elsewhere);
        var user = await _factory.CreateAuthenticatedClient();

        // Act
        var request = await user.Client.GetFromJsonAsync<JsonElement>(
            $"/api/oauth/authorization-requests/{handle}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("elsewhere.acme.example", request.GetProperty("redirectHost").GetString());
        Assert.True(request.GetProperty("redirectHostDiffers").GetBoolean());
    }

    [Fact]
    public async Task Consent_AnswersNotFound_OnceTheRequestHasExpired()
    {
        // Arrange
        await SetFlag(true);
        var client = RegisterClient();
        var handle = await StartAuthorization(client);
        await ChangeAuthorization(client, a => a.ExpiresAt = a.CreatedAt.Minus(Duration.FromSeconds(1)));
        var user = await _factory.CreateAuthenticatedClient();

        // Act
        var read = await user.Client.GetAsync($"/api/oauth/authorization-requests/{handle}", TestContext.Current.CancellationToken);
        var approve = await user.Client.PostAsync($"/api/oauth/authorization-requests/{handle}/approve", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, approve.StatusCode);
    }

    [Fact]
    public async Task Token_RefusesAnExpiredCode()
    {
        // Arrange
        await SetFlag(true);
        var client = RegisterClient();
        var (_, code) = await Approve(client);
        await ChangeAuthorization(client, a => a.CodeExpiresAt = a.DecidedAt!.Value.Minus(Duration.FromSeconds(1)));

        // Act
        var response = await RedeemRequest(client, code);

        // Assert
        await AssertRefused(response, "invalid_grant");
    }

    [Fact]
    public async Task Token_RefusesACode_WhenItsUserHasBeenDeactivated()
    {
        // Arrange
        await SetFlag(true);
        var client = RegisterClient();
        var (user, code) = await Approve(client);
        await SetUserActive(user.UserId, false);

        // Act
        var response = await RedeemRequest(client, code);

        // Assert
        await AssertRefused(response, "invalid_grant");
    }

    [Fact]
    public async Task Refresh_RefusesADeactivatedUser_WithoutSpendingTheToken()
    {
        // Arrange — once the user is enabled again, the connector keeps working
        await SetFlag(true);
        var client = RegisterClient();
        var (user, code) = await Approve(client);
        var refreshToken = (await (await RedeemRequest(client, code)).Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("refresh_token").GetString()!;
        await SetUserActive(user.UserId, false);

        // Act
        var refused = await RefreshRequest(client, refreshToken);

        // Assert
        await AssertRefused(refused, "invalid_grant");
        await SetUserActive(user.UserId, true);
        Assert.Equal(HttpStatusCode.OK, (await RefreshRequest(client, refreshToken)).StatusCode);
    }

    [Fact]
    public async Task Token_AnswersTooManyRequests_PastItsLimit()
    {
        // Arrange — a host of its own, so its limiter starts empty and the lowered limit touches no other test
        await SetFlag(true);
        using var limited = _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(
            [
                new KeyValuePair<string, string?>("RateLimitSettings:OAuthToken:PermitLimit", "2"),
                new KeyValuePair<string, string?>("RateLimitSettings:OAuthToken:WindowSeconds", "3600"),
            ])));
        var client = limited.CreateClient();
        var form = new Dictionary<string, string> { ["grant_type"] = "refresh_token", ["client_id"] = "x", ["refresh_token"] = "x" };
        await client.PostAsync(AuthorizationServerMetadata.TokenPath, new FormUrlEncodedContent(form), TestContext.Current.CancellationToken);
        await client.PostAsync(AuthorizationServerMetadata.TokenPath, new FormUrlEncodedContent(form), TestContext.Current.CancellationToken);

        // Act
        var response = await client.PostAsync(AuthorizationServerMetadata.TokenPath, new FormUrlEncodedContent(form), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.NotNull(response.Headers.RetryAfter);
    }

    [Fact]
    public async Task Authorize_ShowsAServerError_WhenTheWebClientUrlIsNotSet()
    {
        // Arrange
        await SetFlag(true);
        using var unconfigured = _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection([new KeyValuePair<string, string?>("ApiSettings:WebClientUrl", "")])));
        var browser = unconfigured.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Act
        var response = await browser.GetAsync(AuthorizeUrl(RegisterClient()), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Authorize_ShowsAnError_ForAClientIdLongerThanItsColumn()
    {
        // Arrange
        await SetFlag(true);
        var client = RegisterClient();
        var url = AuthorizeUrl(client, p => p["client_id"] = "https://client.acme.example/" + new string('a', 2100));

        // Act
        var response = await Browser().GetAsync(url, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Authorize_PurgesRequestsExpiredOverADayAgo()
    {
        // Arrange
        await SetFlag(true);
        var stale = RegisterClient();
        await StartAuthorization(stale);
        await ChangeAuthorization(stale, a => a.ExpiresAt = a.CreatedAt.Minus(Duration.FromDays(2)));

        // Act
        await StartAuthorization(RegisterClient());

        // Assert
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WaydDbContext>();
        Assert.False(await db.OAuthAuthorizations.AnyAsync(a => a.ClientId == stale.ClientId, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("multipart/form-data; boundary=----x", "------x--")]
    [InlineData("application/x-www-form-urlencoded; charset=not-a-charset", "grant_type=refresh_token")]
    public async Task Token_RefusesABodyItCannotRead_AsAnInvalidRequest(string contentType, string body)
    {
        // Arrange
        await SetFlag(true);
        var content = new StringContent(body);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

        // Act
        var response = await _factory.CreateClient().PostAsync(AuthorizationServerMetadata.TokenPath, content, TestContext.Current.CancellationToken);

        // Assert
        await AssertRefused(response, "invalid_request");
    }

    /// <summary>Changes the authorization request <paramref name="client"/> started, as time passing would.</summary>
    private async Task ChangeAuthorization(OAuthClient client, Action<OAuthAuthorization> change)
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>().SetCurrentUserId("integration-test-harness");
        var db = scope.ServiceProvider.GetRequiredService<WaydDbContext>();
        var authorization = await db.OAuthAuthorizations.SingleAsync(a => a.ClientId == client.ClientId, TestContext.Current.CancellationToken);
        change(authorization);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SetUserActive(string userId, bool active)
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>().SetCurrentUserId("integration-test-harness");
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId) ?? throw new InvalidOperationException($"No user {userId}.");
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().Now;
        if (active)
            user.Activate(EventActor.System, now);
        else
            user.Deactivate(EventActor.System, now);
        var updated = await users.UpdateAsync(user);
        Assert.True(updated.Succeeded);
    }
}
