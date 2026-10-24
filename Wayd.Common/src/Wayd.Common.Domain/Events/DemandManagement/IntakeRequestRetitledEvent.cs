using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// An intake request's title changed.
/// </summary>
/// <remarks>
/// Its own event rather than part of <see cref="IntakeRequestDetailsUpdatedEvent"/> because the title is summary tier and the details are substance: on most desks everyone who can view requests reads the title.
/// </remarks>
public sealed record IntakeRequestRetitledEvent : DomainEvent<IntakeRequestRetitledEvent>, IDomainEventDescriptor, IIntakeRequestEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public static IntakeRequestReadTier ReadTier => IntakeRequestReadTier.Summary;

    public IntakeRequestRetitledEvent(Guid id, int key, string fromTitle, string toTitle, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        FromTitle = fromTitle;
        ToTitle = toTitle;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string FromTitle { get; }
    public string ToTitle { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeRequest";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
