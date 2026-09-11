using System.Text.Json.Serialization;
using Wayd.Common.Models;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A strategic initiative was created in a portfolio.
/// </summary>
/// <remarks>
/// The aggregate is the <em>portfolio</em>, not the initiative: an initiative only exists inside one, it
/// is created and deleted through the portfolio aggregate, and the portfolio's Activity section is where
/// a reader looks to see what was added to it. Should initiatives grow an activity log of their own, that
/// is a second event about their own aggregate rather than a change of this one's.
/// </remarks>
public sealed record StrategicInitiativeCreatedEvent : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public StrategicInitiativeCreatedEvent(
        Guid portfolioId,
        Guid strategicInitiativeId,
        string name,
        LocalDateRange dateRange,
        Dictionary<int, Guid[]>? roles,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        PortfolioId = portfolioId;
        StrategicInitiativeId = strategicInitiativeId;
        Name = name;
        DateRange = dateRange;
        Roles = roles?.ToDictionary(x => x.Key, x => x.Value.ToArray());

        Timestamp = timestamp;
    }

    public Guid PortfolioId { get; }
    public Guid StrategicInitiativeId { get; }
    public string Name { get; }
    public LocalDateRange DateRange { get; }

    /// <summary>
    /// The roles for the initiative. The key is the role type id and the value is an array of employee ids.
    /// </summary>
    public Dictionary<int, Guid[]>? Roles { get; }

    [JsonIgnore]
    public string AggregateType => "ProjectPortfolio";
    [JsonIgnore]
    public Guid AggregateId => PortfolioId;
}
