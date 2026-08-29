using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A release package shipped.
/// </summary>
public sealed record PackageReleasedEvent : DomainEvent, IProductManagementEvent
{
    public PackageReleasedEvent(Guid id, int key, string version, LocalDate releasedDate, int componentCount, Guid statusId, EventActor actor, Instant timestamp)
        : base(actor)
    {
        Id = id;
        Key = key;
        Version = version;
        ReleasedDate = releasedDate;
        ComponentCount = componentCount;
        StatusId = statusId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Version { get; }
    public LocalDate ReleasedDate { get; }
    public int ComponentCount { get; }
    public Guid StatusId { get; }
}
