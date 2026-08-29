using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A release was cut — its scope is fixed and it is ready to ship.
/// </summary>
/// <remarks>
/// Distinct from <see cref="ReleaseReleasedEvent"/> because cut-to-released is the latency measure
/// phase one reports, and it needs both ends as separate facts.
/// </remarks>
public sealed record ReleaseCutEvent : DomainEvent, IProductManagementEvent
{
    public ReleaseCutEvent(Guid id, int key, Guid productId, string productName, string version, LocalDate cutDate, Guid statusId, EventActor actor, Instant timestamp)
        : base(actor)
    {
        Id = id;
        Key = key;
        ProductId = productId;
        ProductName = productName;
        Version = version;
        CutDate = cutDate;
        StatusId = statusId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid ProductId { get; }
    public string ProductName { get; }
    public string Version { get; }
    public LocalDate CutDate { get; }
    public Guid StatusId { get; }
}
