using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A release was announced — it reached customers.
/// </summary>
/// <remarks>
/// Not what deployment frequency counts. That measures artifacts reaching environments and is derived
/// from deployments; this is the moment the market was told, which can be days later or never happen
/// at all for something shipped quietly.
/// </remarks>
public sealed record ReleaseReleasedEvent : DomainEvent, IProductManagementEvent
{
    public ReleaseReleasedEvent(
        Guid id,
        int key,
        Guid? productId,
        string version,
        LocalDate releasedDate,
        int versionCount,
        int packageCount,
        Guid statusId,
        EventActor actor,
        Instant timestamp)
        : base(actor)
    {
        Id = id;
        Key = key;
        ProductId = productId;
        Version = version;
        ReleasedDate = releasedDate;
        VersionCount = versionCount;
        PackageCount = packageCount;
        StatusId = statusId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid? ProductId { get; }
    public string Version { get; }
    public LocalDate ReleasedDate { get; }

    /// <summary>How many versions the release carried directly, outside any package.</summary>
    public int VersionCount { get; }

    /// <summary>How many packages the release shipped.</summary>
    public int PackageCount { get; }

    public Guid StatusId { get; }
}
