using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A release's target date moved.
/// </summary>
/// <remarks>
/// Carries both ends, because here the change is the story: "slipped two weeks" is the fact a watcher
/// wants, and it cannot be recovered from the new value alone.
/// </remarks>
public sealed record ReleaseTargetDateMovedEvent : DomainEvent, IProductManagementEvent
{
    public ReleaseTargetDateMovedEvent(
        Guid id,
        int key,
        Guid? productId,
        string version,
        LocalDate? fromTargetDate,
        LocalDate? toTargetDate,
        EventActor actor,
        Instant timestamp)
        : base(actor)
    {
        Id = id;
        Key = key;
        ProductId = productId;
        Version = version;
        FromTargetDate = fromTargetDate;
        ToTargetDate = toTargetDate;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid? ProductId { get; }
    public string Version { get; }
    public LocalDate? FromTargetDate { get; }
    public LocalDate? ToTargetDate { get; }
}
