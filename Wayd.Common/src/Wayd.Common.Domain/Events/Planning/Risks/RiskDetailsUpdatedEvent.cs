using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Risks;

/// <summary>
/// A risk's written details taken together, as <see cref="RiskDetailsUpdatedEvent.Previous"/> records the values a
/// change replaced.
/// </summary>
public sealed record RiskDetails(string Summary, string? Description, string? Response);

/// <summary>
/// A risk's summary, description or response changed.
/// </summary>
public sealed record RiskDetailsUpdatedEvent : DomainEvent<RiskDetailsUpdatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public RiskDetailsUpdatedEvent(
        Guid id,
        int key,
        string summary,
        string? description,
        string? response,
        RiskDetails? previous,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Summary = summary;
        Description = description;
        Response = response;
        Previous = previous;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Summary { get; }
    public string? Description { get; }

    /// <summary>What has been done to help prevent the risk from occurring.</summary>
    public string? Response { get; }

    /// <summary>
    /// The details this change replaced. Grouped so that null can only mean "not recorded", as
    /// <c>ProjectDetailsUpdatedEvent.Previous</c> is.
    /// </summary>
    public RiskDetails? Previous { get; }

    [JsonIgnore]
    public string AggregateType => "Risk";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
