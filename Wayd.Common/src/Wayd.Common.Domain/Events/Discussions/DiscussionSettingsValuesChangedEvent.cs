using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Discussions;

/// <summary>
/// How conversations behave in one scope, or by default for a channel type, changed. Carries both ends of each
/// setting; on the first save for a scope, the previous values are the ones it inherited.
/// </summary>
/// <remarks>
/// Also listed on the scope's own Activity, such as an intake desk's, so the people who run it see the change there.
/// </remarks>
public sealed record DiscussionSettingsValuesChangedEvent : DomainEvent<DiscussionSettingsValuesChangedEvent>, IDomainEventDescriptor, IRelatedAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public DiscussionSettingsValuesChangedEvent(
        Guid id,
        string channelType,
        string scopeType,
        Guid? scopeId,
        bool fromInternalNotesEnabled,
        bool toInternalNotesEnabled,
        bool fromMentionsGrantAccess,
        bool toMentionsGrantAccess,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        ChannelType = channelType;
        ScopeType = scopeType;
        ScopeId = scopeId;
        FromInternalNotesEnabled = fromInternalNotesEnabled;
        ToInternalNotesEnabled = toInternalNotesEnabled;
        FromMentionsGrantAccess = fromMentionsGrantAccess;
        ToMentionsGrantAccess = toMentionsGrantAccess;

        Timestamp = timestamp;
    }

    /// <summary>The settings row's id.</summary>
    public Guid Id { get; }

    /// <summary>The registered channel type the settings apply to, such as <c>demand.intake-request</c>.</summary>
    public string ChannelType { get; }

    /// <summary>The aggregate type of the scope, such as <c>IntakeDesk</c>.</summary>
    public string ScopeType { get; }

    /// <summary>The scope's record, or null for the channel type's default.</summary>
    public Guid? ScopeId { get; }

    /// <summary>Whether new internal notes could be posted before the change.</summary>
    public bool FromInternalNotesEnabled { get; }

    /// <summary>Whether new internal notes can be posted after the change.</summary>
    public bool ToInternalNotesEnabled { get; }

    /// <summary>Whether a mention could give access before the change.</summary>
    public bool FromMentionsGrantAccess { get; }

    /// <summary>Whether a mention can give access after the change.</summary>
    public bool ToMentionsGrantAccess { get; }

    [JsonIgnore]
    public string AggregateType => "DiscussionSettings";

    [JsonIgnore]
    public Guid AggregateId => Id;

    [JsonIgnore]
    public IReadOnlyCollection<AggregateReference> RelatedAggregates =>
        ScopeId is { } scopeId ? [new(ScopeType, scopeId)] : [];
}
