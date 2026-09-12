using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A product node was pointed at the record that owns it in another system, relinked, or unlinked.
/// </summary>
/// <remarks>
/// Separate from a details change because a consumer does react differently: the link is what lets an
/// integration correlate a repository, pipeline or registry package back to a product, so one arriving
/// or disappearing changes what can be resolved — where a rename changes only what is displayed. Carries
/// both ends, because a consumer that correlated on the old identifier has to know which one stopped
/// resolving.
/// <para>
/// Supersedes <see cref="ProductLinkedExternallyEvent"/>, dropping its required <c>Name</c> and its
/// <c>Description</c>, which described the product rather than the change. A new type rather than a new
/// version, because removing a required member breaks every consumer written against the old shape.
/// </para>
/// </remarks>
public sealed record ProductLinkedExternallyEventV2 : DomainEvent, IProductManagementEvent
{
    [JsonConstructor]
    public ProductLinkedExternallyEventV2(Guid id, int key, string? previousExternalId, string? externalId, EventActor actor, Instant timestamp)
        : base(actor, "2.0")
    {
        Id = id;
        Key = key;
        PreviousExternalId = previousExternalId;
        ExternalId = externalId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The identifier it was linked to before, or <c>null</c> when it was not linked.</summary>
    public string? PreviousExternalId { get; }

    /// <summary>The identifier it is linked to now, or <c>null</c> when the link was cleared.</summary>
    public string? ExternalId { get; }

    [JsonIgnore]
    public string AggregateType => "Product";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
