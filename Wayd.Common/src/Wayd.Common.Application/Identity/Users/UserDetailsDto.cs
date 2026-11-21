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

    /// <summary>The Entra tenant the user's next sign-in is staged to move them to, or their first sign-in is staged from.</summary>
    public string? PendingMigrationTenantId { get; set; }

    /// <summary>When the tenant link was staged.</summary>
    public Instant? PendingMigrationTenantStagedAt { get; set; }

    /// <summary>
    /// When the staged tenant link stops being honoured and must be staged again. Null when none is staged, or when it
    /// was staged before its time was recorded, which counts as expired.
    /// </summary>
    public Instant? PendingMigrationTenantExpiresAt => PendingMigrationTenantId is null ? null : StagedLinks.ExpiresAt(PendingMigrationTenantStagedAt);

    /// <summary>The name of the identity provider the user is staged to move to; their next sign-in through it completes the move.</summary>
    public string? PendingMigrationProviderId { get; set; }

    /// <summary>When the provider move was staged.</summary>
    public Instant? PendingMigrationProviderStagedAt { get; set; }

    /// <summary>
    /// When the staged provider move stops being honoured and must be staged again. Null when none is staged, or when
    /// it was staged before its time was recorded, which counts as expired.
    /// </summary>
    public Instant? PendingMigrationProviderExpiresAt => PendingMigrationProviderId is null ? null : StagedLinks.ExpiresAt(PendingMigrationProviderStagedAt);

    /// <summary>
    /// Whether the user has an identity they can sign in with. False for an admin-created
    /// Entra user who has not yet signed in.
    /// </summary>
    public bool HasActiveIdentity { get; set; }

    public Instant? LastActivityAt { get; set; }

    public NavigationDto? Employee { get; set; }

    public List<RoleListDto> Roles { get; set; } = [];
}

