using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Identity.Roles;

namespace Wayd.Common.Application.Identity.Users;

public sealed record UserDetailsDto
{
    public string Id { get; set; } = null!;

    public string? UserName { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Email { get; set; }

    public bool IsActive { get; set; } = true;

    public Instant? LockoutEnd { get; set; }

    public string? PhoneNumber { get; set; }

    public string LoginProvider { get; set; } = null!;

    public string? PendingMigrationTenantId { get; set; }

    public string? PendingMigrationProviderId { get; set; }

    /// <summary>
    /// Whether the user has an identity they can sign in with. False for an admin-created
    /// Entra user who has not yet signed in.
    /// </summary>
    public bool HasActiveIdentity { get; set; }

    public Instant? LastActivityAt { get; set; }

    public NavigationDto? Employee { get; set; }

    public List<RoleListDto> Roles { get; set; } = [];
}

