using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Identity;
using Wayd.Common.Application.Identity.Roles;
using Wayd.Common.Application.Identity.Tokens;
using Wayd.Common.Application.Identity.Users;
using Wayd.Infrastructure.Auth;

namespace Wayd.Web.Api.IntegrationTests.Infrastructure;

/// <summary>An <see cref="HttpClient"/> that carries a signed-in user's access token.</summary>
public sealed record AuthenticatedClient(string UserId, HttpClient Client);

public static class AuthenticatedClientExtensions
{
    private const string Password = "Integration-Test-Passw0rd";

    /// <summary>
    /// Creates a local user holding exactly the given permissions, signs them in through
    /// <c>POST /api/auth/login</c>, and returns a client that sends their token.
    /// </summary>
    /// <remarks>
    /// Every call makes its own role and user, because the database is shared by the whole collection: a
    /// reused role would carry one test's permissions into the next. Authorization reads a user's
    /// permissions from their roles on each request rather than from the token's claims, so the role is
    /// what has to be right.
    /// </remarks>
    public static async Task<AuthenticatedClient> CreateAuthenticatedClient(
        this WaydSqlServerApiFactory factory, params string[] permissions)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"user-{suffix}@integration.test";

        var client = factory.CreateClient();

        string userId;
        using (var scope = factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>().SetCurrentUserId("integration-test-harness");
            var roleService = scope.ServiceProvider.GetRequiredService<IRoleService>();
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

            var roleName = $"Test Role {suffix}";
            var roleId = await roleService.CreateOrUpdate(new CreateOrUpdateRoleCommand(null, roleName, null));

            var granted = await roleService.UpdatePermissions(new UpdateRolePermissionsCommand(roleId, [.. permissions]), cancellationToken);
            if (granted.IsFailure)
                throw new InvalidOperationException($"Could not grant the test role its permissions: {granted.Error}");

            var created = await userService.CreateAsync(new CreateUserCommand
            {
                FirstName = "Integration",
                LastName = "Test",
                Email = email,
                LoginProvider = LoginProviders.Wayd,
                Password = Password,
                MustChangePassword = false,
                RoleNames = [roleName],
            }, cancellationToken);
            if (created.IsFailure)
                throw new InvalidOperationException($"Could not create the test user: {created.Error}");

            userId = created.Value;
        }

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginCommand(email, Password), cancellationToken);
        login.EnsureSuccessStatusCode();
        var token = await login.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The login response had no body.");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        return new AuthenticatedClient(userId, client);
    }
}
