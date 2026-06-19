using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Wayd.Infrastructure.Auth.Bootstrap;

internal static class BootstrapService
{
    /// <summary>
    /// Called once after database initialization. If no users exist, generates
    /// a one-time setup token and logs it prominently so the operator can hit
    /// POST /api/auth/setup to create the first admin account.
    /// </summary>
    internal static async Task RunAsync(
        IServiceProvider services,
        BootstrapTokenService tokenService,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        if (await userManager.Users.AnyAsync(cancellationToken))
            return;

        var token = tokenService.Generate();

        logger.LogWarning(
            """

            ============================================================
             WAYD FIRST-RUN SETUP
            ============================================================
             No users exist. Use the token below to create the first
             admin account.

             UI:    <your-app-url>/setup
             API:   POST /api/auth/setup

             Token: {Token}

             This token is valid for the lifetime of this process only.
             Restart the application to generate a new token.
            ============================================================
            """,
            token);
    }
}
