using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement.Incidents;

/// <summary>An incident's title, symptoms, root cause, or severity changed.</summary>
/// <remarks>
/// Supersedes <see cref="IncidentDetailsUpdatedEvent"/>, which carried one description. Symptoms are what
/// responders are seeing; root cause is why, and stays null until someone records it. Removing the old
/// description member breaks every consumer written against that shape, so this is a new type.
/// </remarks>
public sealed record IncidentDetailsUpdatedEventV2 : DomainEvent<IncidentDetailsUpdatedEventV2>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public IncidentDetailsUpdatedEventV2(
        Guid id,
        int key,
        string fromTitle,
        string? fromSymptoms,
        string? fromRootCause,
        Guid fromSeverityLevelId,
        string toTitle,
        string? toSymptoms,
        string? toRootCause,
        Guid toSeverityLevelId,
        EventActor actor,
        Instant timestamp)
        : base(actor, "2.0")
    {
        Id = id;
        Key = key;
        FromTitle = fromTitle;
        FromSymptoms = fromSymptoms;
        FromRootCause = fromRootCause;
        FromSeverityLevelId = fromSeverityLevelId;
        ToTitle = toTitle;
        ToSymptoms = toSymptoms;
        ToRootCause = toRootCause;
        ToSeverityLevelId = toSeverityLevelId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string FromTitle { get; }
    public string? FromSymptoms { get; }
    public string? FromRootCause { get; }
    public Guid FromSeverityLevelId { get; }
    public string ToTitle { get; }
    public string? ToSymptoms { get; }
    public string? ToRootCause { get; }
    public Guid ToSeverityLevelId { get; }

    [JsonIgnore]
    public string AggregateType => "Incident";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
