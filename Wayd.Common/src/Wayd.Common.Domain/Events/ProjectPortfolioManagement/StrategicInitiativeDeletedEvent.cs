using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A strategic initiative was deleted from a portfolio.
/// </summary>
/// <remarks>
/// The aggregate is the initiative — see <see cref="StrategicInitiativeCreatedEvent"/>. The name is carried
/// because the row it describes is gone by the time anyone reads the entry.
/// </remarks>
public sealed record StrategicInitiativeDeletedEvent : DomainEvent<StrategicInitiativeDeletedEvent>, IDomainEventDescriptor, IPpmEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Removed;

    [JsonConstructor]
    public StrategicInitiativeDeletedEvent(
        Guid portfolioId,
        Guid strategicInitiativeId,
        int key,
        string name,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.1")
    {
        PortfolioId = portfolioId;
        StrategicInitiativeId = strategicInitiativeId;
        Key = key;
        Name = name;

        Timestamp = timestamp;
    }

    public Guid PortfolioId { get; }
    public Guid StrategicInitiativeId { get; }

    /// <summary>Added in 1.1. Zero on a payload written before it.</summary>
    public int Key { get; }

    public string Name { get; }

    [JsonIgnore]
    public string AggregateType => "StrategicInitiative";
    [JsonIgnore]
    public Guid AggregateId => StrategicInitiativeId;
}
