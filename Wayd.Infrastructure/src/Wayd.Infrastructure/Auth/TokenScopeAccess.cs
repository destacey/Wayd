using System.Security.Claims;

namespace Wayd.Infrastructure.Auth;

/// <summary>
/// Applies the scope a caller's token carries (<see cref="TokenScopes.ClaimType"/>) to a permission check. Every
/// permission check intersects the user's permissions with this, so a scope can only narrow access.
/// </summary>
/// <remarks>
/// A principal with no scope claim is not limited: a sign-in to the web app carries none. Every token that can be
/// limited carries one, so a scope claim naming nothing Wayd defines permits nothing rather than everything.
/// </remarks>
public static class TokenScopeAccess
{
    /// <summary>Whether <paramref name="principal"/>'s token scope allows <paramref name="permission"/>.</summary>
    public static bool Permits(ClaimsPrincipal? principal, string permission) =>
        Limit(principal) switch
        {
            { Limited: false } => true,
            { Scope: { } scope } => scope.Permits(permission),
            _ => false,
        };

    /// <summary>
    /// Whether <paramref name="principal"/>'s token is limited to less than <see cref="TokenScope.Write"/>, so it may
    /// not change anything.
    /// </summary>
    public static bool IsReadOnly(ClaimsPrincipal? principal) =>
        Limit(principal) is { Limited: true } limit && limit.Scope is not TokenScope.Write;

    /// <summary>
    /// The scope <paramref name="principal"/>'s authenticated identities carry, and whether they carry one at all.
    /// Should more than one carry a scope, the narrowest holds; one naming no scope Wayd defines limits it to nothing.
    /// </summary>
    private static (bool Limited, TokenScope? Scope) Limit(ClaimsPrincipal? principal)
    {
        var claims = principal?.Identities
            .Where(i => i.IsAuthenticated)
            .SelectMany(i => i.FindAll(TokenScopes.ClaimType))
            .ToList();
        if (claims is null || claims.Count == 0)
            return (false, null);

        TokenScope? narrowest = null;
        foreach (var claim in claims)
        {
            if (TokenScopes.Parse(claim.Value) is not { } scope)
                return (true, null);
            if (narrowest is null || scope < narrowest)
                narrowest = scope;
        }

        return (true, narrowest);
    }
}
