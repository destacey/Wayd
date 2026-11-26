using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Wayd.Common.Application.Identity;
using Wayd.Common.Application.Identity.Roles;
using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Domain.Authorization;
using Wayd.Infrastructure;
using Wayd.Infrastructure.Auth;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// A user with a temporary password holds tokens that can only replace it, so the password an administrator chose,
/// and whoever else saw it, can do nothing else. Checked against the booted host, since it is the policies the
/// endpoints actually run that decide.
/// </summary>
[Collection(SignInApiTestCollection.Name)]
public sealed class PasswordChangeRequirementTests(WaydSqlServerApiFactory factory)
{
    private const string TemporaryPassword = "Temporary-Passw0rd-1";
    private const string ChosenPassword = "Chosen-Passw0rd-2";
    private static readonly string ViewUsers = ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Users);

    private readonly WaydSqlServerApiFactory _factory = factory;

    /// <summary>The ids of the users <see cref="SignInWithATemporaryPassword"/> created, by email.</summary>
    private readonly Dictionary<string, string> _userIds = [];

    [Fact]
    public async Task ATemporaryPasswordsToken_IsRefusedEndpointsItsUserMayOtherwiseCall()
    {
        // Arrange — the user holds the permission, so only the restriction can refuse
        var (_, client) = await SignInWithATemporaryPassword(ViewUsers);

        // Act
        var signInOnly = await client.GetAsync("/api/user-management/profiles", TestContext.Current.CancellationToken);
        var permissionGated = await client.GetAsync("/api/user-management/users", TestContext.Current.CancellationToken);

        // Assert
        signInOnly.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        permissionGated.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AChangedPassword_SignsInToATokenThoseEndpointsAccept()
    {
        // Arrange
        var (email, client) = await SignInWithATemporaryPassword(ViewUsers);
        var changed = await client.PutAsJsonAsync("/api/user-management/profiles/change-password",
            new { currentPassword = TemporaryPassword, newPassword = ChosenPassword }, TestContext.Current.CancellationToken);

        // Act
        var signedIn = await SignIn(email, ChosenPassword);
        var signInOnly = await signedIn.GetAsync("/api/user-management/profiles", TestContext.Current.CancellationToken);
        var permissionGated = await signedIn.GetAsync("/api/user-management/users", TestContext.Current.CancellationToken);

        // Assert
        changed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        signInOnly.StatusCode.Should().Be(HttpStatusCode.OK);
        permissionGated.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/api/auth/logout")]
    [InlineData("/api/auth/logout-all")]
    public async Task ATemporaryPasswordsToken_CanSignOut(string path)
    {
        // Arrange
        var (_, client) = await SignInWithATemporaryPassword();

        // Act
        var signedOut = await client.PostAsJsonAsync(path, new { refreshToken = "" }, TestContext.Current.CancellationToken);

        // Assert
        signedOut.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task APersonalAccessToken_IsRestricted_WhileItsOwnersPasswordIsTemporary()
    {
        // Arrange — the token is minted first; an administrator's reset leaves it in place
        var (email, client) = await SignInWithATemporaryPassword(ViewUsers);
        await client.PutAsJsonAsync("/api/user-management/profiles/change-password",
            new { currentPassword = TemporaryPassword, newPassword = ChosenPassword }, TestContext.Current.CancellationToken);
        var owner = await SignIn(email, ChosenPassword);
        var created = await owner.PostAsJsonAsync("/api/user-management/personal-access-tokens",
            new { name = "before-the-reset", expiresAt = SystemClock.Instance.GetCurrentInstant().Plus(Duration.FromDays(7)).ToString() }, TestContext.Current.CancellationToken);
        var pat = (await created.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("token").GetString();
        var withToken = _factory.CreateClient();
        withToken.DefaultRequestHeaders.Add(AuthConstants.ApiKeyHeaderName, pat);
        await ResetPassword(email, TemporaryPassword);

        // Act
        var whileTemporary = await withToken.GetAsync("/api/user-management/users", TestContext.Current.CancellationToken);
        var reset = await SignIn(email, TemporaryPassword);
        await reset.PutAsJsonAsync("/api/user-management/profiles/change-password",
            new { currentPassword = TemporaryPassword, newPassword = "Chosen-Again-Passw0rd-3" }, TestContext.Current.CancellationToken);
        var afterChange = await withToken.GetAsync("/api/user-management/users", TestContext.Current.CancellationToken);

        // Assert
        whileTemporary.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        afterChange.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Creates a local user whose password is a temporary one, holding <paramref name="permissions"/> through a role of
    /// their own, signs them in, and answers their email and client.
    /// </summary>
    private async Task<(string Email, HttpClient Client)> SignInWithATemporaryPassword(params string[] permissions)
    {
        var email = $"temporary-{Guid.NewGuid():N}@integration.test";
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ActAsHarnessAdministrator();
            var roleName = $"Temporary Password Role {Guid.NewGuid():N}";
            var roles = scope.ServiceProvider.GetRequiredService<IRoleService>();
            var roleId = await roles.CreateOrUpdate(new CreateOrUpdateRoleCommand(null, roleName, null));
            (await roles.UpdatePermissions(new UpdateRolePermissionsCommand(roleId, [.. permissions]), TestContext.Current.CancellationToken))
                .IsSuccess.Should().BeTrue();
            var created = await scope.ServiceProvider.GetRequiredService<IUserService>().CreateAsync(new CreateUserCommand
            {
                FirstName = "Temporary",
                LastName = "Password",
                Email = email,
                LoginProvider = LoginProviders.Wayd,
                Password = TemporaryPassword,
                MustChangePassword = true,
                RoleNames = [roleName],
            }, TestContext.Current.CancellationToken);
            created.IsSuccess.Should().BeTrue(created.IsFailure ? created.Error : null);
            _userIds[email] = created.Value;
        }

        var login = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { userName = email, password = TemporaryPassword }, TestContext.Current.CancellationToken);
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("mustChangePassword").GetBoolean().Should().BeTrue();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());
        return (email, client);
    }

    /// <summary>Signs <paramref name="email"/> in with <paramref name="password"/> and answers a client sending that sign-in's token.</summary>
    private async Task<HttpClient> SignIn(string email, string password)
    {
        var login = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { userName = email, password }, TestContext.Current.CancellationToken);
        login.EnsureSuccessStatusCode();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            (await login.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("token").GetString());
        return client;
    }

    /// <summary>Has an administrator set <paramref name="email"/>'s password to <paramref name="password"/>, a temporary one.</summary>
    private async Task ResetPassword(string email, string password)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ActAsHarnessAdministrator();
        var users = scope.ServiceProvider.GetRequiredService<IUserService>();
        (await users.ResetPasswordAsync(new ResetPasswordCommand(_userIds[email], password), TestContext.Current.CancellationToken)).IsSuccess.Should().BeTrue();
    }
}
