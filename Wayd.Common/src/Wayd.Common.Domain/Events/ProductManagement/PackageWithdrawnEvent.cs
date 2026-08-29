using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A release package was pulled after being assembled.
/// </summary>
public sealed record PackageWithdrawnEvent : DomainEvent, IProductManagementEvent
{
    public PackageWithdrawnEvent(Guid id, int key, string version, string? reason, Guid statusId, EventActor actor, Instant timestamp)
        : base(actor)
    {
        Id = id;
        Key = key;
        Version = version;
        Reason = reason;
        StatusId = statusId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Version { get; }
    public string? Reason { get; }
    public Guid StatusId { get; }
}
