using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A strategic initiative was deleted from a portfolio.
/// </summary>
/// <remarks>
/// The aggregate is the portfolio — see <see cref="StrategicInitiativeCreatedEvent"/>. The name is carried
/// because the row it describes is gone by the time anyone reads the entry.
/// </remarks>
public sealed record StrategicInitiativeDeletedEvent : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public StrategicInitiativeDeletedEvent(
        Guid portfolioId,
        Guid strategicInitiativeId,
        string name,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        PortfolioId = portfolioId;
        StrategicInitiativeId = strategicInitiativeId;
        Name = name;

        Timestamp = timestamp;
    }

    public Guid PortfolioId { get; }
    public Guid StrategicInitiativeId { get; }
    public string Name { get; }

    [JsonIgnore]
    public string AggregateType => "ProjectPortfolio";
    [JsonIgnore]
    public Guid AggregateId => PortfolioId;
}
