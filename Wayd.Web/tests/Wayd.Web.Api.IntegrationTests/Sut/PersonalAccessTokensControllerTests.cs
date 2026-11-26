using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using NodaTime;
using Wayd.Infrastructure;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// A personal access token is created or changed only from a sign-in to the web app: a token that could mint or
/// extend tokens would let a leaked one outlive its own revocation.
/// </summary>
[Collection(SignInApiTestCollection.Name)]
public sealed class PersonalAccessTokensControllerTests(WaydSqlServerApiFactory factory)
{
    private const string Path = "/api/user-management/personal-access-tokens";
    private readonly WaydSqlServerApiFactory _factory = factory;

    [Fact]
    public async Task Create_SucceedsFromASignIn_AndIsRefusedToAToken()
    {
        // Arrange
        var user = await _factory.CreateAuthenticatedClient();
        var (_, token) = await CreateToken(user.Client);
        var withToken = _factory.CreateClient();
        withToken.DefaultRequestHeaders.Add(AuthConstants.ApiKeyHeaderName, token);

        // Act
        var minted = await withToken.PostAsJsonAsync(Path, NewToken("minted-by-a-token"), TestContext.Current.CancellationToken);

        // Assert
        minted.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_IsRefusedToAToken_EvenForItself()
    {
        // Arrange — a token extending its own expiry would never need replacing
        var user = await _factory.CreateAuthenticatedClient();
        var (id, token) = await CreateToken(user.Client);
        var withToken = _factory.CreateClient();
        withToken.DefaultRequestHeaders.Add(AuthConstants.ApiKeyHeaderName, token);
        var extended = new { name = "extended", expiresAt = SystemClock.Instance.GetCurrentInstant().Plus(Duration.FromDays(300)).ToString() };

        // Act
        var byToken = await withToken.PutAsJsonAsync($"{Path}/{id}", extended, TestContext.Current.CancellationToken);
        var bySignIn = await user.Client.PutAsJsonAsync($"{Path}/{id}", extended, TestContext.Current.CancellationToken);

        // Assert
        byToken.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        bySignIn.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private static object NewToken(string name) =>
        new { name, expiresAt = SystemClock.Instance.GetCurrentInstant().Plus(Duration.FromDays(7)).ToString() };

    /// <summary>Creates a token from <paramref name="signedIn"/>'s sign-in and answers its id and value.</summary>
    private static async Task<(Guid Id, string Token)> CreateToken(HttpClient signedIn)
    {
        var created = await signedIn.PostAsJsonAsync(Path, NewToken("created-by-a-sign-in"), TestContext.Current.CancellationToken);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return (body.GetProperty("id").GetGuid(), body.GetProperty("token").GetString()!);
    }
}
