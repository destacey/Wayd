namespace Wayd.Common.Application.Identity.Users;

/// <summary>
/// A user eligible to be bulk-migrated off a given source tenant: bound to the
/// provider via an active identity on that tenant, with no migration already pending.
/// </summary>
public sealed record TenantMigrationCandidateDto
{
    public required string UserId { get; init; }

    public string? UserName { get; init; }

    public string? Email { get; init; }

    public string? FirstName { get; init; }

    public string? LastName { get; init; }

    public bool IsActive { get; init; }
}
