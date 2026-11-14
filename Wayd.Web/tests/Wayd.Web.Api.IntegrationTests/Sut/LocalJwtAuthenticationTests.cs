using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
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
