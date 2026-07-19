using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Wayd.Common.Application.Identity;

namespace Wayd.Infrastructure.Auth;

/// <summary>
/// Pure identity: "who is the caller?" (claims, ids, roles). Permission checks live on
/// <see cref="CurrentPrincipal"/> — they need <see cref="IUserService"/>, which depends on this
/// class, so hosting them here would recreate the CurrentUser ↔ UserService cycle.
/// </summary>
public class CurrentUser(IHttpContextAccessor httpContextAccessor, AmbientUserId ambientUserId) : ICurrentUser, ICurrentUserInitializer
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly AmbientUserId _ambientUserId = ambientUserId;

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    /// <summary>
    /// Who is acting: an authenticated HTTP user or a background scope seeded with a user id is a
    /// <see cref="ActorKind.User"/>; a live HTTP request with no authenticated user is a genuine
    /// <see cref="ActorKind.Anonymous"/> caller; and no HTTP request with no seeded user means the
    /// platform itself is acting (<see cref="ActorKind.System"/> — scheduled jobs, durable message
    /// delivery, startup work).
    /// </summary>
    public ActorKind Kind
    {
        get
        {
            if (IsAuthenticated())
                return ActorKind.User;

            var ambientId = _ambientUserId.Value;
            if (!string.IsNullOrEmpty(ambientId))
                return SystemIdentity.IsSystem(ambientId) ? ActorKind.System : ActorKind.User;

            return _httpContextAccessor.HttpContext is null ? ActorKind.System : ActorKind.Anonymous;
        }
    }

    public string? Name =>
        Kind == ActorKind.System ? SystemIdentity.Name : User?.Identity?.Name;

    public string GetUserId() =>
        IsAuthenticated()
            ? User?.GetUserId() ?? string.Empty
            : Kind == ActorKind.System
                ? SystemIdentity.UserId
                : _ambientUserId.Value ?? string.Empty;

    public Guid? GetEmployeeId()
    {
        if (IsAuthenticated())
        {
            var employeeId = User?.GetEmployeeId();
            if (Guid.TryParse(employeeId, out var employeeGuid))
                return employeeGuid;
        }

        return null;
    }

    public string? GetUserEmail() =>
        IsAuthenticated()
            ? User!.GetEmail()
            : string.Empty;

    public bool IsAuthenticated() =>
        User?.Identity?.IsAuthenticated is true;

    public void SetCurrentUserId(string userId) =>
        // Stored on the scoped AmbientUserId (shared with the handler's DbContext in the same scope)
        // rather than a private field, so Wolverine's identity middleware and the handler that runs in
        // the same message scope both see it. Idempotent for the same id; throws only on a conflicting
        // change (preserving the old "reserved for in-scope initialization" invariant).
        _ambientUserId.Set(userId);
}
