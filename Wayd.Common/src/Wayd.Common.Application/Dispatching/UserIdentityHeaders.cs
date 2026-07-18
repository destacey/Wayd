namespace Wayd.Common.Application.Dispatching;

/// <summary>
/// Well-known Wolverine envelope header keys used to carry the acting user's identity across a
/// message send. Wolverine runs every message in a fresh DI scope, so — unlike MediatR, which reused
/// the caller's scope — the current user does not automatically flow to handlers dispatched outside
/// an HTTP request (e.g. Hangfire jobs). The dispatcher stamps this header on send and identity
/// middleware restores it into the handler's scope.
/// </summary>
public static class UserIdentityHeaders
{
    /// <summary>Header carrying the acting user's id (<see cref="ICurrentUser.GetUserId"/>).</summary>
    public const string UserId = "wayd-user-id";
}
