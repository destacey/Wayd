namespace Wayd.Web.Api.Models.UserManagement.Users;

/// <summary>
/// Identifies which of the caller's sessions a request concerns. Optional: without it, logout
/// falls back to revoking every session and the sessions list cannot mark "this device".
/// </summary>
public sealed record LogoutRequest
{
    public string? RefreshToken { get; set; }
}
