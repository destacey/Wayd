using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement.Incidents;

/// <summary>Who reported an incident changed.</summary>
public sealed record IncidentReportedByChangedEvent
    : DomainEvent<IncidentReportedByChangedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public IncidentReportedByChangedEvent(
        Guid id,
        int key,
        Guid fromReportedByEmployeeId,
        Guid toReportedByEmployeeId,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        FromReportedByEmployeeId = fromReportedByEmployeeId;
        ToReportedByEmployeeId = toReportedByEmployeeId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid FromReportedByEmployeeId { get; }
    public Guid ToReportedByEmployeeId { get; }

    [JsonIgnore]
    public string AggregateType => "Incident";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
