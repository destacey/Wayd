using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// The versions or packages a release carries changed.
/// </summary>
/// <remarks>
/// One event for both, because a release's contents are one set however they were reached: a consumer
/// asking "what does 2026.07 announce" gets the same answer whether a version arrived directly or
/// inside a package. Carries counts rather than ids — a consumer needing the membership reads it, and
/// an event that enumerated it would go stale against the next amendment.
/// </remarks>
public sealed record ReleaseContentsChangedEvent : DomainEvent, IProductManagementEvent
{
    public ReleaseContentsChangedEvent(Guid id, int key, string version, int versionCount, int packageCount, EventActor actor, Instant timestamp)
        : base(actor)
    {
        Id = id;
        Key = key;
        Version = version;
        VersionCount = versionCount;
        PackageCount = packageCount;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Version { get; }

    /// <summary>How many versions the release carries directly, outside any package.</summary>
    public int VersionCount { get; }

    /// <summary>How many packages the release ships.</summary>
    public int PackageCount { get; }
}
