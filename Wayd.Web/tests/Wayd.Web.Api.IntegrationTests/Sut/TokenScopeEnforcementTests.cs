using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Authorization;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Asserts, against the booted host, that a personal access token's scope narrows what it can do: a read-only token
/// keeps its user's view permissions, loses every permission that changes data, and cannot change anything through an
/// endpoint that asks for sign-in alone.
/// </summary>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class TokenScopeEnforcementTests(WaydSqlServerApiFactory factory)
{
    private const string Portfolios = "/api/ppm/portfolios";
    private const string PersonalAccessTokens = "/api/user-management/personal-access-tokens";

    private static readonly string[] PortfolioPermissions =
    [
        ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.ProjectPortfolios),
        ApplicationPermission.NameFor(ApplicationAction.Create, ApplicationResource.ProjectPortfolios),
    ];

    private readonly WaydSqlServerApiFactory _factory = factory;

    [Fact]
    public async Task ReadToken_CanUseAViewPermission()
    {
        // Arrange
        var client = await ClientWithToken(TokenScope.Read);

        // Act
        var response = await client.GetAsync(Portfolios, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(TokenScope.Read, HttpStatusCode.Forbidden)]
    [InlineData(TokenScope.Write, HttpStatusCode.Created)]
    public async Task Token_CanUseACreatePermissionItsUserHolds_OnlyWithWriteScope(TokenScope scope, HttpStatusCode expected)
    {
        // Arrange
        var client = await ClientWithToken(scope);

        // Act
        var response = await client.PostAsJsonAsync(
            Portfolios,
            new { name = $"Scope {Guid.NewGuid():N}"[..20], description = "Created by the token scope tests." },
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(expected, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(TokenScope.Read, HttpStatusCode.Forbidden)]
    [InlineData(TokenScope.Write, HttpStatusCode.Created)]
    public async Task Token_CanChangeDataBehindSignInAlone_OnlyWithWriteScope(TokenScope scope, HttpStatusCode expected)
    {
        // Arrange — creating a personal access token needs no permission, only a signed-in user
        var client = await ClientWithToken(scope);

        // Act
        var response = await client.PostAsJsonAsync(PersonalAccessTokens, TokenRequest(TokenScope.Write), TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(expected, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("PUT", "")]
    [InlineData("POST", "/participants")]
    [InlineData("POST", "/status")]
    [InlineData("POST", "/withdraw")]
    public async Task ReadToken_IsRefusedAWriteEndpointGatedByAViewPermission(string method, string suffix)
    {
        // Arrange — a requester may change their own intake request under the view permission alone
        var client = await ClientWithToken(TokenScope.Read, ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.IntakeRequests));
        var request = new HttpRequestMessage(new HttpMethod(method), $"/api/demand-management/intake-requests/{Guid.NewGuid()}{suffix}")
        {
            Content = JsonContent.Create(new { }),
        };

        // Act
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReadToken_CanPostToAnActionMarkedAsChangingNothing()
    {
        // Arrange — listing sessions is a POST because the body carries a credential
        var client = await ClientWithToken(TokenScope.Read);

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/sessions", new { }, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadToken_ReportsOnlyThePermissionsItCanUse()
    {
        // Arrange
        var client = await ClientWithToken(TokenScope.Read);

        // Act
        var response = await client.GetFromJsonAsync<JsonElement>("/api/user-management/profiles/permissions", TestContext.Current.CancellationToken);

        // Assert
        response.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).Should()
            .Contain(PortfolioPermissions[0])
            .And.NotContain(PortfolioPermissions[1]);
    }

    [Fact]
    public async Task CreatedToken_ReportsItsScope()
    {
        // Arrange
        var user = await _factory.CreateAuthenticatedClient();

        // Act
        var created = await user.Client.PostAsJsonAsync(PersonalAccessTokens, TokenRequest(TokenScope.Read), TestContext.Current.CancellationToken);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("id").GetGuid();
        var token = await user.Client.GetFromJsonAsync<JsonElement>($"{PersonalAccessTokens}/{id}", TestContext.Current.CancellationToken);

        // Assert
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        token.GetProperty("scope").GetString().Should().Be(nameof(TokenScope.Read));
    }

    /// <summary>
    /// A client authenticating with a new token, limited to <paramref name="scope"/>, of a user holding
    /// <paramref name="permissions"/>, or <see cref="PortfolioPermissions"/> when none are named.
    /// </summary>
    private async Task<HttpClient> ClientWithToken(TokenScope scope, params string[] permissions)
    {
        var user = await _factory.CreateAuthenticatedClient(permissions.Length > 0 ? permissions : PortfolioPermissions);
        var response = await user.Client.PostAsJsonAsync(PersonalAccessTokens, TokenRequest(scope), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("token").GetString()!;

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("x-api-key", token);
        return client;
    }

    private static object TokenRequest(TokenScope scope) => new
    {
        name = $"Scope {Guid.NewGuid():N}"[..20],
        expiresAt = SystemClock.Instance.GetCurrentInstant().Plus(Duration.FromDays(7)).ToString(),
        scope = scope.ToString(),
    };
}
