using NodaTime;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A release package was assembled from a set of component releases.
/// </summary>
public sealed record PackageAssembledEvent : DomainEvent, IProductManagementEvent
{
    public PackageAssembledEvent(Guid id, int key, string version, string? name, int componentCount, int changedCount, Guid statusId, StatusCategory statusCategory, EventActor actor, Instant timestamp)
        : base(actor)
    {
        Id = id;
        Key = key;
        Version = version;
        Name = name;
        ComponentCount = componentCount;
        ChangedCount = changedCount;
        StatusId = statusId;
        StatusCategory = statusCategory;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The package's own version, distinct from any component's. Free text, never parsed.</summary>
    public string Version { get; }

    public string? Name { get; }

    /// <summary>How many components the manifest records, changed and carried forward together.</summary>
    public int ComponentCount { get; }

    /// <summary>How many of those actually changed in this package.</summary>
    public int ChangedCount { get; }

    public Guid StatusId { get; }
    public StatusCategory StatusCategory { get; }
}
