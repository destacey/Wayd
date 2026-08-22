namespace Wayd.Web.Api.Models.UserManagement.Users;

/// <summary>
/// Identifies which of the caller's sessions a request concerns. Optional, but a request that
/// omits it does less: logout revokes nothing, and the sessions list cannot mark "this device".
/// Use <c>logout-all</c> to end every session.
/// </summary>
public sealed record LogoutRequest
{
    public string? RefreshToken { get; set; }
}
