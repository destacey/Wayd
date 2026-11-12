using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement.Incidents;

/// <summary>Who commands an incident's response changed. Either end is null when nobody is assigned.</summary>
public sealed record IncidentCommanderChangedEvent
    : DomainEvent<IncidentCommanderChangedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public IncidentCommanderChangedEvent(
        Guid id,
        int key,
        Guid? fromCommanderEmployeeId,
        Guid? toCommanderEmployeeId,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        FromCommanderEmployeeId = fromCommanderEmployeeId;
        ToCommanderEmployeeId = toCommanderEmployeeId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid? FromCommanderEmployeeId { get; }
    public Guid? ToCommanderEmployeeId { get; }

    [JsonIgnore]
    public string AggregateType => "Incident";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
