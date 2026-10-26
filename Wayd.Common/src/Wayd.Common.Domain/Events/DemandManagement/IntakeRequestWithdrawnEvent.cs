using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// The requester withdrew their intake request: they no longer need it.
/// </summary>
/// <remarks>
/// Its own event rather than a status change, because it is the requester's decision rather than the desk's,
/// and it is the only way a request reaches Withdrawn. Summary tier; a note the requester left is substance,
/// recorded on the status transition and on the request.
/// </remarks>
public sealed record IntakeRequestWithdrawnEvent : DomainEvent<IntakeRequestWithdrawnEvent>, IDomainEventDescriptor, IIntakeRequestEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StatusChanged;

    public static IntakeRequestReadTier ReadTier => IntakeRequestReadTier.Summary;

    public IntakeRequestWithdrawnEvent(Guid id, int key,
        Guid fromStatusId, StatusCategory fromCategory, IntakeRequestStatusAlias fromAlias,
        Guid toStatusId, StatusCategory toCategory,
        EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        FromStatusId = fromStatusId;
        FromCategory = fromCategory;
        FromAlias = fromAlias;
        ToStatusId = toStatusId;
        ToCategory = toCategory;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid FromStatusId { get; }
    public StatusCategory FromCategory { get; }
    public IntakeRequestStatusAlias FromAlias { get; }

    /// <summary>The desk workflow's status meaning Withdrawn.</summary>
    public Guid ToStatusId { get; }
    public StatusCategory ToCategory { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeRequest";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
