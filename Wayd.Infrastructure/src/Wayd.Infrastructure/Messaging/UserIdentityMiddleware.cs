using Wayd.Common.Application.Dispatching;
using Wolverine;

namespace Wayd.Infrastructure.Messaging;

/// <summary>
/// Wolverine middleware that restores the acting user id from the message envelope into the handler's
/// DI scope.
/// </summary>
/// <remarks>
/// Wolverine executes every message in a fresh DI scope, so the scoped <see cref="ICurrentUser"/> the
/// handler resolves is a different instance from the one on the sending scope. For HTTP-originated
/// sends this is invisible — <c>CurrentUser</c> lazily reads <c>IHttpContextAccessor</c>, which flows
/// with the async call chain regardless of scope. But for sends with no ambient <c>HttpContext</c>
/// (Hangfire jobs and durable event handlers) the fresh <c>CurrentUser</c> would resolve a blank user and
/// silently drop audit attribution. The dispatcher stamps <see cref="UserIdentityHeaders.UserId"/>
/// onto the envelope on send; this middleware reads it back and seeds the fresh scope's
/// <see cref="ICurrentUserInitializer"/>, mirroring what <c>WaydJobActivator</c> does for Hangfire.
/// </remarks>
public static class UserIdentityMiddleware
{
    public static void Before(Envelope envelope, AmbientUserId ambientUserId)
    {
        if (envelope.Headers.TryGetValue(UserIdentityHeaders.UserId, out var userId)
            && !string.IsNullOrEmpty(userId))
        {
            // Seed the scoped ambient id from the message header so the handler's CurrentUser (and thus
            // its WaydDbContext auditing) sees the acting user. Safe on the HTTP path — an authenticated
            // HttpContext user still wins in CurrentUser.GetUserId(); this only seeds the fallback.
            ambientUserId.Set(userId);
        }
    }
}
