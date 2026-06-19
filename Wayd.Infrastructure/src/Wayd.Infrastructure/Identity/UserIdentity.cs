using NodaTime;

namespace Wayd.Infrastructure.Identity;

public class UserIdentity
{
    public Guid Id { get; set; }

    public string UserId { get; set; } = null!;

    public ApplicationUser? User { get; set; }

    public string Provider { get; set; } = null!;

    public string? ProviderTenantId { get; set; }

    public string ProviderSubject { get; set; } = null!;

    public bool IsActive { get; set; }

    public Instant LinkedAt { get; set; }

    public Instant? UnlinkedAt { get; set; }

    public string? UnlinkReason { get; set; }
}

public static class UserIdentityUnlinkReasons
{
    public const string TenantMigration = "TenantMigration";
    public const string AdminRevoked = "AdminRevoked";
    public const string UserUnlinked = "UserUnlinked";
    public const string ProviderRelinked = "ProviderRelinked";
}
