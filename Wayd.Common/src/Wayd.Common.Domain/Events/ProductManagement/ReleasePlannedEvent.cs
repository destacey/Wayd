using NodaTime;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A release was planned — an announcement drafted, before it carries anything.
/// </summary>
/// <remarks>
/// Contents arrive later, so this says nothing about them. Unlike a package, which is assembled from
/// its manifest in one act, a release is commonly drafted before anyone knows which versions will
/// make it.
/// </remarks>
public sealed record ReleasePlannedEvent : DomainEvent, IProductManagementEvent
{
    public ReleasePlannedEvent(
        Guid id,
        int key,
        Guid? productId,
        string version,
        string? name,
        LocalDate? targetDate,
        Guid statusId,
        StatusCategory statusCategory,
        EventActor actor,
        Instant timestamp)
        : base(actor)
    {
        Id = id;
        Key = key;
        ProductId = productId;
        Version = version;
        Name = name;
        TargetDate = targetDate;
        StatusId = statusId;
        StatusCategory = statusCategory;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>
    /// The product node the release is announced under, where it is scoped to one.
    /// </summary>
    /// <remarks>
    /// Nullable, and carries no product name for that reason: a release spanning product lines has no
    /// single owner to name, so a consumer that wants one resolves it rather than reading it here.
    /// </remarks>
    public Guid? ProductId { get; }

    /// <summary>The release's own version label as entered. Free text, never parsed.</summary>
    public string Version { get; }

    public string? Name { get; }
    public LocalDate? TargetDate { get; }
    public Guid StatusId { get; }
    public StatusCategory StatusCategory { get; }
}
