namespace Wayd.Web.Api.Models.UserManagement.Users;

public sealed record StageSignInTenantRequest
{
    /// <summary>
    /// The tenant the user signs in from. Optional when the Entra provider allows a single tenant.
    /// </summary>
    public string? TenantId { get; set; }
}
