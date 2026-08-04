namespace Wayd.Common.Application.Interfaces;

/// <summary>
/// Pure identity: who is the caller (id, name, email, employee link) — nothing about what they may
/// do. Authorization questions belong to <see cref="ICurrentPrincipal"/>; raw claims stay behind
/// the auth infrastructure and are deliberately not exposed here.
/// </summary>
public interface ICurrentUser
{
    ActorKind Kind { get; }

    string? Name { get; }

    string GetUserId();

    /// <summary>
    /// The employee link as carried by the caller's token, or <c>null</c> when absent.
    /// </summary>
    /// <remarks>
    /// This is a <em>snapshot</em>, not the current value: a JWT carries the link as it stood at
    /// sign-in (so linking a user has no effect until they re-authenticate) and a personal access
    /// token carries it as it stood at token creation, for that token's whole lifetime. It is also
    /// always <c>null</c> off the HTTP path, since background scopes have no claims. Use
    /// <see cref="ICurrentPrincipal.GetEmployeeId"/> wherever the answer gates behaviour; this
    /// remains for cheap, non-authoritative reads such as echoing the claim back to the client.
    /// </remarks>
    Guid? GetEmployeeId();

    string? GetUserEmail();

    bool IsAuthenticated();
}
