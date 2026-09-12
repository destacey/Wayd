using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// The tags on a product node changed.
/// </summary>
/// <remarks>
/// Carries both the change and its result. <see cref="Added"/> and <see cref="Removed"/> are the fact, for a
/// consumer that reacts to it — applying a tag on a single-value axis replaces the one it held, so one call
/// can both add and remove. <see cref="TagIds"/> is the full set afterwards, for a consumer that keeps a
/// copy: applying the latest set is correct however deliveries were ordered or repeated, and applying the
/// deltas is not.
/// <para>
/// Supersedes <see cref="ProductTagsChangedEvent"/>, dropping its required <c>Name</c>, which described the
/// product rather than the change. A new type rather than a new version, because removing a required member
/// breaks every consumer written against the old shape.
/// </para>
/// </remarks>
public sealed record ProductTagsChangedEventV2 : DomainEvent, IProductManagementEvent
{
    [JsonConstructor]
    public ProductTagsChangedEventV2(Guid id, int key, Guid[] added, Guid[] removed, Guid[] tagIds, EventActor actor, Instant timestamp)
        : base(actor, "2.0")
    {
        Id = id;
        Key = key;
        Added = [.. added];
        Removed = [.. removed];
        TagIds = [.. tagIds];

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The tags this change applied.</summary>
    public Guid[] Added { get; }

    /// <summary>The tags this change took off, including any a single-value axis replaced.</summary>
    public Guid[] Removed { get; }

    /// <summary>Every tag the product carries after the change.</summary>
    public Guid[] TagIds { get; }

    [JsonIgnore]
    public string AggregateType => "Product";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
