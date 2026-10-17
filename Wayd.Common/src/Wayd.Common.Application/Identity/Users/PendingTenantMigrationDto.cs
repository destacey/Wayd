namespace Wayd.Common.Application.Identity.Users;

/// <summary>
/// A user on the provider with a staged-but-not-yet-completed tenant migration.
/// <c>sourceTenantId</c> is the user's current tenant; <c>targetTenantId</c> is the staged
/// destination they'll rebind to on their next sign-in.
/// </summary>
/// <remarks>
/// Drives the read-only "Active migrations" view.
/// </remarks>
public sealed record PendingTenantMigrationDto
{
    public required string UserId { get; init; }

    public string? UserName { get; init; }

    public string? Email { get; init; }

    /// <summary>The tenant of the user's active sign-in identity, or null when the user has no active identity.</summary>
    /// <remarks>Read from the user's active <c>UserIdentity</c> row.</remarks>
    public string? SourceTenantId { get; init; }

    public required string TargetTenantId { get; init; }

    public Instant? StagedAt { get; init; }
}
