using Wayd.Common.Models;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// Tracking began for a strategic initiative that existed before <see cref="StrategicInitiativeCreatedEvent"/>
/// was recorded. Carries that event's payload, describing the initiative as it stood at
/// <see cref="DomainEvent.Timestamp"/>.
/// </summary>
public sealed record StrategicInitiativeBaselinedEvent : BaselineEvent<StrategicInitiativeBaselinedEvent, StrategicInitiativeCreatedEvent>
{
    public StrategicInitiativeBaselinedEvent(Guid portfolioId, Guid strategicInitiativeId, int key, string name, string? description, int status, LocalDateRange dateRange, Dictionary<int, Guid[]>? roles, Instant? recordCreatedOn, Guid? recordCreatedById, Instant timestamp)
        : base("StrategicInitiative", strategicInitiativeId, recordCreatedOn, recordCreatedById, timestamp, "1.1")
    {
        PortfolioId = portfolioId;
        StrategicInitiativeId = strategicInitiativeId;
        Key = key;
        Name = name;
        Description = description;
        Status = status;
        DateRange = dateRange;
        Roles = roles?.ToDictionary(x => x.Key, x => x.Value.ToArray());
    }

    public Guid PortfolioId { get; }
    public Guid StrategicInitiativeId { get; }
    public int Key { get; }
    public string Name { get; }
    public string? Description { get; }
    public int Status { get; }
    public LocalDateRange DateRange { get; }

    /// <summary>The role type id mapped to the ids of the employees holding it.</summary>
    public Dictionary<int, Guid[]>? Roles { get; }
}
