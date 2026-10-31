using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Discussions;

/// <summary>
/// A scope's saved discussion settings were removed because the scope itself, such as an intake desk, was deleted.
/// </summary>
/// <remarks>
/// Also listed on the scope's own Activity, beside the scope's deletion.
/// </remarks>
public sealed record DiscussionSettingsDeletedEvent : DomainEvent<DiscussionSettingsDeletedEvent>, IDomainEventDescriptor, IRelatedAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Removed;

    public DiscussionSettingsDeletedEvent(Guid id, string channelType, string scopeType, Guid scopeId, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        ChannelType = channelType;
        ScopeType = scopeType;
        ScopeId = scopeId;

        Timestamp = timestamp;
    }

    /// <summary>The settings row's id.</summary>
    public Guid Id { get; }

    /// <summary>The registered channel type the settings applied to, such as <c>demand.intake-request</c>.</summary>
    public string ChannelType { get; }

    /// <summary>The aggregate type of the scope, such as <c>IntakeDesk</c>.</summary>
    public string ScopeType { get; }

    /// <summary>The deleted scope's record.</summary>
    public Guid ScopeId { get; }

    [JsonIgnore]
    public string AggregateType => "DiscussionSettings";

    [JsonIgnore]
    public Guid AggregateId => Id;

    [JsonIgnore]
    public IReadOnlyCollection<AggregateReference> RelatedAggregates => [new(ScopeType, ScopeId)];
}
