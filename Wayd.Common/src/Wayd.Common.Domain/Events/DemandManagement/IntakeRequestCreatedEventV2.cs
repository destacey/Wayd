using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// An intake request was submitted to a desk.
/// </summary>
/// <remarks>
/// Replaces <see cref="IntakeRequestCreatedEvent"/>, whose channel was required.
/// <para>
/// Carries the summary tier only, because the activity log is filtered by tier: the description, expected benefits, submitter and origin are substance. The submitter and origin never change, so the record holds them; the description as submitted is the first <see cref="IntakeRequestDetailsUpdatedEvent"/>'s previous value.
/// </para>
/// </remarks>
public sealed record IntakeRequestCreatedEventV2 : DomainEvent<IntakeRequestCreatedEventV2>, IDomainEventDescriptor, IIntakeRequestEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    public static IntakeRequestReadTier ReadTier => IntakeRequestReadTier.Summary;

    public IntakeRequestCreatedEventV2(Guid id, int key, string title, Guid deskId, Guid? channelId, Guid requesterId, IntakeRequestNeedByDetails? needBy,
        Guid statusId, StatusCategory statusCategory, IntakeRequestStatusAlias statusAlias, EventActor actor, Instant timestamp)
        : base(actor, "2.0")
    {
        Id = id;
        Key = key;
        Title = title;
        DeskId = deskId;
        ChannelId = channelId;
        RequesterId = requesterId;
        NeedBy = needBy;
        StatusId = statusId;
        StatusCategory = statusCategory;
        StatusAlias = statusAlias;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Title { get; }
    public Guid DeskId { get; }
    /// <summary>Where the request came from, or null when it was submitted to a desk that offers no channel.</summary>
    public Guid? ChannelId { get; }
    /// <summary>The employee the request is for.</summary>
    public Guid RequesterId { get; }
    /// <summary>The need-by date with its basis, when one was given.</summary>
    public IntakeRequestNeedByDetails? NeedBy { get; }

    /// <summary>The status the request was submitted in.</summary>
    public Guid StatusId { get; }

    /// <summary>The category of that status.</summary>
    public StatusCategory StatusCategory { get; }

    /// <summary>The meaning of that status, always Submitted.</summary>
    public IntakeRequestStatusAlias StatusAlias { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeRequest";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
