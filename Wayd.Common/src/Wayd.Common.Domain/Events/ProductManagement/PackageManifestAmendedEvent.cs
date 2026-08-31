using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A release package's manifest changed after it was assembled.
/// </summary>
/// <remarks>
/// Worth its own type because the manifest is the record that answers "what was running on this date".
/// An amendment after the fact means an earlier answer to that question was wrong, which is a different
/// thing from the package itself changing state.
/// </remarks>
public sealed record PackageManifestAmendedEvent : DomainEvent, IProductManagementEvent
{
    public PackageManifestAmendedEvent(Guid id, int key, string version, int componentCount, int changedCount, EventActor actor, Instant timestamp)
        : base(actor)
    {
        Id = id;
        Key = key;
        Version = version;
        ComponentCount = componentCount;
        ChangedCount = changedCount;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Version { get; }
    public int ComponentCount { get; }
    public int ChangedCount { get; }
}
