namespace Wayd.Common.Domain.Events;

/// <summary>
/// The one <see cref="DomainEvent.EventId"/> a record's baseline can have.
/// </summary>
public static class BaselineEventId
{
    // Changing this gives every record a second baseline id, so a rewritten baseline would no longer
    // collide with the one already in the log.
    private static readonly Guid Namespace = new("5b0e7c2a-3f6d-4c8e-9a1b-7d2f4e6a8c30");

    /// <summary>
    /// A name-based UUID of <paramref name="aggregateType"/> and <paramref name="aggregateId"/>. The type is
    /// part of the name because replicas share their source record's id across modules.
    /// </summary>
    public static Guid For(string aggregateType, Guid aggregateId) =>
        NameBasedUuid.Create(Namespace, $"{aggregateType}:{aggregateId:D}");
}
