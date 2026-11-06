using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A project deliverable moved to another status of its workflow: started, accepted, descoped or reopened.
/// </summary>
/// <remarks>
/// Carries both ends with their names as they read at the time, so a copy kept in another module — an intake
/// request deciding whether it has been met — can show the status without loading the workflow, and a later
/// rename cannot rewrite it.
/// </remarks>
public sealed record ProjectDeliverableStatusChangedEvent : DomainEvent<ProjectDeliverableStatusChangedEvent>, IDomainEventDescriptor, IPpmEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ProjectDeliverableStatusChangedEvent(
        Guid id,
        ProjectKey key,
        Guid deliverableId,
        Guid fromStatusId,
        string fromStatusName,
        StatusCategory fromCategory,
        ProjectDeliverableStatusAlias fromAlias,
        Guid toStatusId,
        string toStatusName,
        StatusCategory toCategory,
        ProjectDeliverableStatusAlias toAlias,
        string? reason,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        DeliverableId = deliverableId;
        FromStatusId = fromStatusId;
        FromStatusName = fromStatusName;
        FromCategory = fromCategory;
        FromAlias = fromAlias;
        ToStatusId = toStatusId;
        ToStatusName = toStatusName;
        ToCategory = toCategory;
        ToAlias = toAlias;
        Reason = reason;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public ProjectKey Key { get; }

    /// <summary>The deliverable this event is about.</summary>
    public Guid DeliverableId { get; }

    public Guid FromStatusId { get; }
    public string FromStatusName { get; }
    public StatusCategory FromCategory { get; }
    public ProjectDeliverableStatusAlias FromAlias { get; }
    public Guid ToStatusId { get; }
    public string ToStatusName { get; }
    public StatusCategory ToCategory { get; }
    public ProjectDeliverableStatusAlias ToAlias { get; }

    /// <summary>Why, when the move needed or was given one; required for a Removed status such as Descoped.</summary>
    public string? Reason { get; }

    [JsonIgnore]
    public string AggregateType => "Project";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
