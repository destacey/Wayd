namespace Wayd.Common.Application.Identity.Users;

public sealed record UserIdentityDto
{
    public Guid Id { get; set; }

    public string Provider { get; set; } = null!;

    public string? ProviderTenantId { get; set; }

    public string ProviderSubject { get; set; } = null!;

    public bool IsActive { get; set; }

    public Instant LinkedAt { get; set; }

    public Instant? UnlinkedAt { get; set; }

    public string? UnlinkReason { get; set; }
}
