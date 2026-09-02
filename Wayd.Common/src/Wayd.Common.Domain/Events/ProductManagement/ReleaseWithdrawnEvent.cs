using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A release was retracted after being announced.
/// </summary>
/// <remarks>
/// Says nothing about the versions it carried. An artifact that shipped has shipped whatever the
/// market was later told, so a consumer must not infer that the contents were pulled — each version
/// records its own withdrawal where it too was pulled.
/// </remarks>
public sealed record ReleaseWithdrawnEvent : DomainEvent, IProductManagementEvent
{
    public ReleaseWithdrawnEvent(Guid id, int key, Guid? productId, string version, string? reason, Guid statusId, EventActor actor, Instant timestamp)
        : base(actor)
    {
        Id = id;
        Key = key;
        ProductId = productId;
        Version = version;
        Reason = reason;
        StatusId = statusId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid? ProductId { get; }
    public string Version { get; }

    /// <summary>Why it was retracted, where someone recorded a reason.</summary>
    public string? Reason { get; }

    public Guid StatusId { get; }
}
