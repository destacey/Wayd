using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;

namespace Wayd.Common.Domain.Events.ProductManagement.Incidents;

/// <summary>An incident was opened.</summary>
public sealed record IncidentCreatedEvent : DomainEvent<IncidentCreatedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    [JsonConstructor]
    public IncidentCreatedEvent(
        Guid id,
        int key,
        string title,
        Guid submittedByEmployeeId,
        Guid reportedByEmployeeId,
        Guid? commanderEmployeeId,
        Guid severityLevelId,
        Instant noticedAt,
        Instant? startedAt,
        Instant? recoveredAt,
        ProductStatusAlias statusAlias,
        EventActor actor,
        Instant timestamp,
        string? symptoms,
        string? remediation,
        string? rootCause,
        IReadOnlyCollection<Guid> productIds,
        IReadOnlyCollection<Guid> releaseIds)
        : base(actor, "1.1")
    {
        Id = id;
        Key = key;
        Title = title;
        SubmittedByEmployeeId = submittedByEmployeeId;
        ReportedByEmployeeId = reportedByEmployeeId;
        CommanderEmployeeId = commanderEmployeeId;
        SeverityLevelId = severityLevelId;
        NoticedAt = noticedAt;
        StartedAt = startedAt;
        RecoveredAt = recoveredAt;
        StatusAlias = statusAlias;
        Symptoms = symptoms;
        Remediation = remediation;
        RootCause = rootCause;
        ProductIds = productIds;
        ReleaseIds = releaseIds;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Title { get; }

    /// <summary>The employee who opened the incident. Set once, when it is created.</summary>
    public Guid SubmittedByEmployeeId { get; }

    /// <summary>The employee who reported the incident.</summary>
    public Guid ReportedByEmployeeId { get; }

    /// <summary>The employee commanding the response, when one is assigned.</summary>
    public Guid? CommanderEmployeeId { get; }
    public Guid SeverityLevelId { get; }
    public Instant NoticedAt { get; }
    public Instant? StartedAt { get; }
    public Instant? RecoveredAt { get; }
    public ProductStatusAlias StatusAlias { get; }

    /// <summary>What responders were seeing when the incident was opened. Null when nothing was recorded.</summary>
    public string? Symptoms { get; }

    /// <summary>What was done to restore service, if that was already known. Null until a remediation is recorded.</summary>
    public string? Remediation { get; }

    /// <summary>Why it happened, if that was already known. Null until a cause is recorded.</summary>
    public string? RootCause { get; }

    /// <summary>The products implicated when the incident was opened. Empty when none were linked.</summary>
    public IReadOnlyCollection<Guid> ProductIds { get; }

    /// <summary>The releases the incident pointed at when it was opened. Empty when none were linked.</summary>
    public IReadOnlyCollection<Guid> ReleaseIds { get; }

    [JsonIgnore]
    public string AggregateType => "Incident";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
