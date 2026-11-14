using System.Net;
using System.Security.Claims;
using System.Text;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events;
using Wayd.Infrastructure.Auth;
using Wayd.Infrastructure.Identity;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// A web sign-in's access token is self-contained, so only the scheme looking its user up on each request stops a
/// deactivated user before the token expires, on endpoints that ask for no permission as on those that do.
/// </summary>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class LocalJwtAuthenticationTests(WaydSqlServerApiFactory factory)
{
    private readonly WaydSqlServerApiFactory _factory = factory;

    [Fact]
    public async Task AnAccessToken_StopsWorking_OnceItsUserIsDeactivated()
    {
        // Arrange — deactivated directly, so no session ends and only the scheme's own check can refuse
        var user = await _factory.CreateAuthenticatedClient();
        var before = await user.Client.GetAsync("/api/user-management/profiles", TestContext.Current.CancellationToken);
        await SetUserActive(user.UserId, false);

        // Act
        var after = await user.Client.GetAsync("/api/user-management/profiles", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task AnAccessToken_WorksAgain_OnceItsUserIsReactivated()
    {
        // Arrange
        var user = await _factory.CreateAuthenticatedClient();
        await SetUserActive(user.UserId, false);
        await SetUserActive(user.UserId, true);

        // Act
        var response = await user.Client.GetAsync("/api/user-management/profiles", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AnAccessToken_StopsWorkingOnEveryDevice_OnceTheUserChangesTheirPassword()
    {
        // Arrange — the same user signed in twice: the change is made from one, the other is someone else's
        var user = await _factory.CreateAuthenticatedClient();
        var otherDevice = await SignInAgain(user);
        var before = await otherDevice.GetAsync("/api/user-management/profiles", TestContext.Current.CancellationToken);

        // Act
        var changed = await user.Client.PutAsJsonAsync("/api/user-management/profiles/change-password",
            new { currentPassword = AuthenticatedClientExtensions.Password, newPassword = "Changed-Passw0rd-1" }, TestContext.Current.CancellationToken);
        var after = await otherDevice.GetAsync("/api/user-management/profiles", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Theory]
    [InlineData(true, HttpStatusCode.OK)]
    [InlineData(false, HttpStatusCode.Unauthorized)]
    public async Task AnAccessToken_IsAcceptedOnlyWithTheUsersCredentialVersion(bool carriesIt, HttpStatusCode expected)
    {
        // Arrange — the same hand-built token either way, so only the claim decides; without it, as issued before tokens carried one
        var user = await _factory.CreateAuthenticatedClient();
        var settings = _factory.Services.GetRequiredService<IConfiguration>().GetSection("SecuritySettings:LocalJwt");
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.UserId) };
        if (carriesIt)
        {
            using var scope = _factory.Services.CreateScope();
            var stamp = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(user.UserId))!.SecurityStamp;
            claims.Add(new Claim(CredentialVersion.ClaimType, CredentialVersion.Of(stamp)));
        }
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = settings["Issuer"],
            Audience = settings["Audience"],
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings["Secret"]!)), SecurityAlgorithms.HmacSha256),
            TokenType = "JWT",
        });
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/user-management/profiles", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, response.StatusCode);
    }

    /// <summary>Signs <paramref name="user"/> in again, as from another device, and answers a client sending that sign-in's token.</summary>
    private async Task<HttpClient> SignInAgain(AuthenticatedClient user)
    {
        var profile = await user.Client.GetFromJsonAsync<JsonElement>("/api/user-management/profiles", TestContext.Current.CancellationToken);
        var login = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { userName = profile.GetProperty("email").GetString(), password = AuthenticatedClientExtensions.Password }, TestContext.Current.CancellationToken);
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("token").GetString();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Activates or deactivates <paramref name="userId"/> without going through the service, so their sessions stay live.</summary>
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
        Assert.True((await users.UpdateAsync(user)).Succeeded);
    }
}
