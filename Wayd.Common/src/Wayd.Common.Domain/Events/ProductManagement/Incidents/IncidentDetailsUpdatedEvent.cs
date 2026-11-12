using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement.Incidents;

/// <summary>An incident's title, symptoms, remediation, root cause, or severity changed.</summary>
/// <remarks>
/// Symptoms are what responders are seeing. Remediation is what was done to restore service.
/// Root cause is why, and stays null until someone records it.
/// </remarks>
public sealed record IncidentDetailsUpdatedEvent : DomainEvent<IncidentDetailsUpdatedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public IncidentDetailsUpdatedEvent(
        Guid id,
        int key,
        string fromTitle,
        string? fromSymptoms,
        string? fromRemediation,
        string? fromRootCause,
        Guid fromSeverityLevelId,
        string toTitle,
        string? toSymptoms,
        string? toRemediation,
        string? toRootCause,
        Guid toSeverityLevelId,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.1")
    {
        Id = id;
        Key = key;
        FromTitle = fromTitle;
        FromSymptoms = fromSymptoms;
        FromRemediation = fromRemediation;
        FromRootCause = fromRootCause;
        FromSeverityLevelId = fromSeverityLevelId;
        ToTitle = toTitle;
        ToSymptoms = toSymptoms;
        ToRemediation = toRemediation;
        ToRootCause = toRootCause;
        ToSeverityLevelId = toSeverityLevelId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string FromTitle { get; }
    public string? FromSymptoms { get; }
    public string? FromRemediation { get; }
    public string? FromRootCause { get; }
    public Guid FromSeverityLevelId { get; }
    public string ToTitle { get; }
    public string? ToSymptoms { get; }
    public string? ToRemediation { get; }
    public string? ToRootCause { get; }
    public Guid ToSeverityLevelId { get; }

    [JsonIgnore]
    public string AggregateType => "Incident";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
