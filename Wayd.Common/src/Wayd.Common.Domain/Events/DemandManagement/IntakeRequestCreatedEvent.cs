using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// An intake request was submitted to a desk.
/// </summary>
/// <remarks>
/// Carries the summary tier only, because the activity log is filtered by tier: the description, expected benefits, submitter and origin are substance. The submitter and origin never change, so the record holds them; the description as submitted is the first <see cref="IntakeRequestDetailsUpdatedEvent"/>'s previous value.
/// </remarks>
public sealed record IntakeRequestCreatedEvent : DomainEvent<IntakeRequestCreatedEvent>, IDomainEventDescriptor, IIntakeRequestEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    public static IntakeRequestReadTier ReadTier => IntakeRequestReadTier.Summary;

    public IntakeRequestCreatedEvent(Guid id, int key, string title, Guid deskId, Guid channelId, Guid requesterId, IntakeRequestNeedByDetails? needBy, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Title = title;
        DeskId = deskId;
        ChannelId = channelId;
        RequesterId = requesterId;
        NeedBy = needBy;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Title { get; }
    public Guid DeskId { get; }
    public Guid ChannelId { get; }
    /// <summary>The employee the request is for.</summary>
    public Guid RequesterId { get; }
    /// <summary>The need-by date with its basis, when one was given.</summary>
    public IntakeRequestNeedByDetails? NeedBy { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeRequest";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
