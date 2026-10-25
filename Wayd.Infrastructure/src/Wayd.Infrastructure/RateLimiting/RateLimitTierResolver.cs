using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wayd.Common.Domain.Authorization;

namespace Wayd.Infrastructure.RateLimiting;

/// <summary>Which per-user bucket a signed-in user is held to.</summary>
internal enum RateLimitTier
{
    /// <summary><see cref="RateLimitSettings.PerUser"/>, for everyone without the elevated permission.</summary>
    Standard,

    /// <summary><see cref="RateLimitSettings.PerUserElevated"/>, for a holder of <c>Permissions.RateLimits.Elevated</c>.</summary>
    Elevated,
}

/// <summary>
/// Works out a signed-in user's rate-limit tier from their permissions and remembers it per user, so the per-user
/// limiter, whose partition function cannot wait on the database, finds it ready in <see cref="HttpContext.Items"/>.
/// </summary>
/// <remarks>
/// Permissions are read from the database rather than the token's claims: personal access tokens and delegated MCP
/// tokens carry none, and the web app's can be out of date. Remembering the tier per user keeps that read to one per
/// <see cref="RateLimitSettings.TierCacheSeconds"/>, at the cost of a grant or revoke taking that long to apply.
/// </remarks>
internal sealed class RateLimitTierResolver(
    IMemoryCache cache,
    IUserService userService,
    IOptionsMonitor<RateLimitSettings> settings,
    ILogger<RateLimitTierResolver> logger)
{
    /// <summary>The <see cref="HttpContext.Items"/> key the resolved tier is stored under.</summary>
    internal static readonly object ItemKey = new();

    /// <summary>The permission that selects <see cref="RateLimitTier.Elevated"/>.</summary>
    internal static readonly string ElevatedPermission =
        ApplicationPermission.NameFor(ApplicationAction.Elevated, ApplicationResource.RateLimits);

    private readonly IMemoryCache _cache = cache;
    private readonly IUserService _userService = userService;
    private readonly IOptionsMonitor<RateLimitSettings> _settings = settings;
    private readonly ILogger<RateLimitTierResolver> _logger = logger;

    /// <summary>
    /// The tier for <paramref name="user"/>: standard without a database read for an anonymous request, otherwise the
    /// remembered tier or, failing that, one read from the user's permissions. A read that fails gives the standard tier
    /// and is not remembered, so the tier never fails a request and the next request reads again.
    /// </summary>
    internal async ValueTask<RateLimitTier> Resolve(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (UserIdOf(user) is not { } userId)
            return RateLimitTier.Standard;

        var key = CacheKey(userId);
        if (_cache.TryGetValue<RateLimitTier>(key, out var cached))
            return cached;

        if (await Read(userId, cancellationToken) is not { } tier)
            return RateLimitTier.Standard;

        var lifetime = _settings.CurrentValue.TierCacheSeconds;
        if (lifetime > 0)
            _cache.Set(key, tier, TimeSpan.FromSeconds(lifetime));

        return tier;
    }

    /// <summary>The tier a request was given by the middleware, or standard when none was resolved.</summary>
    internal static RateLimitTier TierOf(HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out var tier) && tier is RateLimitTier resolved ? resolved : RateLimitTier.Standard;

    /// <summary>The signed-in user's id, or null for a request no authorization policy has authenticated.</summary>
    internal static string? UserIdOf(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true && user.GetUserId() is { Length: > 0 } id ? id : null;

    /// <summary>
    /// Reads the tier from the user's permissions, or null when they could not be read. A user who no longer exists gets
    /// the standard tier; their request is refused by whatever needs the user, not here.
    /// </summary>
    /// <remarks>A cancelled request is let through as cancelled, so the client disconnect is handled as one.</remarks>
    private async Task<RateLimitTier?> Read(string userId, CancellationToken cancellationToken)
    {
        try
        {
            return await _userService.HasPermissionAsync(userId, ElevatedPermission, cancellationToken)
                ? RateLimitTier.Elevated
                : RateLimitTier.Standard;
        }
        catch (NotFoundException)
        {
            return RateLimitTier.Standard;
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            _logger.LogWarning(ex, "Could not read the rate-limit tier for user {UserId}; holding the request to the standard tier.", userId);
            return null;
        }
    }

    private static string CacheKey(string userId) => $"rate-limit-tier:{userId}";
}
