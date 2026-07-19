using System.Security.Claims;
using Microsoft.AspNetCore.Http;

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

    private ClaimsPrincipal? _user;

    // Lazily access user from HttpContext when available, otherwise use _user set via SetCurrentUser
    private ClaimsPrincipal? User => _user ?? _httpContextAccessor.HttpContext?.User;

    public string? Name => User?.Identity?.Name;

    public string GetUserId() =>
        IsAuthenticated()
            ? User?.GetUserId() ?? string.Empty
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

    public void SetCurrentUser(ClaimsPrincipal user)
    {
        if (_user != null)
        {
            throw new Exception("Method reserved for in-scope initialization");
        }

        _user = user;
    }

    public void SetCurrentUserId(string userId) =>
        // Stored on the scoped AmbientUserId (shared with the handler's DbContext in the same scope)
        // rather than a private field, so Wolverine's identity middleware and the handler that runs in
        // the same message scope both see it. Idempotent for the same id; throws only on a conflicting
        // change (preserving the old "reserved for in-scope initialization" invariant).
        _ambientUserId.Set(userId);
}