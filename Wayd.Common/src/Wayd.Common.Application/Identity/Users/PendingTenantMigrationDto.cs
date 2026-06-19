namespace Wayd.Common.Application.Identity.Users;

/// <summary>
/// A user on the provider with a staged-but-not-yet-completed tenant migration.
/// Drives the read-only "Active migrations" view. <see cref="SourceTenantId"/> is the
/// user's current active-identity tenant; <see cref="TargetTenantId"/> is the staged
/// destination they'll rebind to on next sign-in.
/// </summary>
public sealed record PendingTenantMigrationDto
{
    public required string UserId { get; init; }

    public string? UserName { get; init; }

    public string? Email { get; init; }

    /// <summary>Current tenant, from the user's active <c>UserIdentity</c> row. Null if none active.</summary>
    public string? SourceTenantId { get; init; }

    public required string TargetTenantId { get; init; }

    public Instant? StagedAt { get; init; }
}
