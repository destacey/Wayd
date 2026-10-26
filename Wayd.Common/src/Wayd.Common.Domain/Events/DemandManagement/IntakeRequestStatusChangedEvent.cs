using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// An agent moved an intake request to another status of its desk's workflow, or pointed a duplicate at a
/// different surviving request.
/// </summary>
/// <remarks>
/// Summary tier: the status is part of the summary, and so is which request a duplicate gives way to. The
/// reason the requester reads is substance, so it is not here; it is recorded on the status transition and on
/// the request. The surviving request is carried as an id only, never its number or title, so the event says
/// nothing about a request its reader cannot see.
/// </remarks>
public sealed record IntakeRequestStatusChangedEvent : DomainEvent<IntakeRequestStatusChangedEvent>, IDomainEventDescriptor, IIntakeRequestEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StatusChanged;

    public static IntakeRequestReadTier ReadTier => IntakeRequestReadTier.Summary;

    public IntakeRequestStatusChangedEvent(Guid id, int key,
        Guid fromStatusId, StatusCategory fromCategory, IntakeRequestStatusAlias fromAlias,
        Guid toStatusId, StatusCategory toCategory, IntakeRequestStatusAlias toAlias,
        Guid? fromDuplicateOfId, Guid? toDuplicateOfId,
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
        ToAlias = toAlias;
        FromDuplicateOfId = fromDuplicateOfId;
        ToDuplicateOfId = toDuplicateOfId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid FromStatusId { get; }
    public StatusCategory FromCategory { get; }
    public IntakeRequestStatusAlias FromAlias { get; }
    public Guid ToStatusId { get; }
    public StatusCategory ToCategory { get; }
    public IntakeRequestStatusAlias ToAlias { get; }

    /// <summary>The request this one was a duplicate of before the change, or null when it was not a duplicate.</summary>
    public Guid? FromDuplicateOfId { get; }

    /// <summary>The request this one is a duplicate of after the change, or null when it is not a duplicate.</summary>
    public Guid? ToDuplicateOfId { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeRequest";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
