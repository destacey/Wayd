using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wayd.Common.Application.Identity.Bootstrap;
using Wayd.Infrastructure.Auth.Bootstrap;
using Wayd.Infrastructure.Auth.Local;
using Wayd.Infrastructure.Auth.Oidc;
using Wayd.Infrastructure.Auth.Permissions;
using Wayd.Infrastructure.Auth.PersonalAccessToken;

namespace Wayd.Infrastructure.Auth;

internal static class ConfigureServices
{
    internal static IServiceCollection AddAuth(this IServiceCollection services, IConfiguration config, IHostEnvironment environment)
    {
        services.AddSingleton<BootstrapTokenService>();
        services.AddSingleton<IBootstrapTokenService>(sp => sp.GetRequiredService<BootstrapTokenService>());

        return services
            .AddCurrentUser()
            .AddPermissions()

            // Must add identity before adding auth!
            .AddIdentity()
            .AddLocalJwtAuth(config, environment)
            .AddOidcProviderRegistry()
            .AddPersonalAccessTokenAuth()
            .AddAuthorizationPolicies();
    }

    private static IServiceCollection AddAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            // Default policy is dynamically determined by PermissionPolicyProvider
            // based on the presence of the x-api-key header to avoid unnecessary PAT authentication attempts
        });

        return services;
    }

    private static IServiceCollection AddCurrentUser(this IServiceCollection services) =>
        services
            .AddHttpContextAccessor()
            // Scoped: shared by CurrentUser and the handler's DbContext within one message/request scope.
            // The message header carries the id across the send→handle scope boundary (see AmbientUserId).
            .AddScoped<AmbientUserId>()
            .AddScoped<ICurrentUser, CurrentUser>()
            .AddScoped(sp => (ICurrentUserInitializer)sp.GetRequiredService<ICurrentUser>());

    private static IServiceCollection AddPermissions(this IServiceCollection services) =>
        services
            .AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>()
            .AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>()
            .AddScoped<IAuthorizationHandler, AnyPermissionAuthorizationHandler>();
}