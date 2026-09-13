using System.Text.Json.Serialization;
using Wayd.Common.Models;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A strategic initiative was created in a portfolio.
/// </summary>
/// <remarks>
/// The aggregate is the initiative. The portfolio is its parent container, as it is for a program or a
/// project, so the entry belongs in the initiative's own Activity section. Entries written before 1.1
/// declared the portfolio and stay in its section: the aggregate type is recorded on the entry, not read
/// from the payload.
/// </remarks>
public sealed record StrategicInitiativeCreatedEvent : DomainEvent<StrategicInitiativeCreatedEvent>, IDomainEventDescriptor, IPpmEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    [JsonConstructor]
    public StrategicInitiativeCreatedEvent(
        Guid portfolioId,
        Guid strategicInitiativeId,
        int key,
        string name,
        string? description,
        int status,
        LocalDateRange dateRange,
        Dictionary<int, Guid[]>? roles,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.1")
    {
        PortfolioId = portfolioId;
        StrategicInitiativeId = strategicInitiativeId;
        Key = key;
        Name = name;
        Description = description;
        Status = status;
        DateRange = dateRange;
        Roles = roles?.ToDictionary(x => x.Key, x => x.Value.ToArray());

        Timestamp = timestamp;
    }

    public Guid PortfolioId { get; }
    public Guid StrategicInitiativeId { get; }

    /// <summary>Added in 1.1. Zero on a payload written before it.</summary>
    public int Key { get; }

    public string Name { get; }

    /// <summary>Added in 1.1. Null on a payload written before it; a created initiative always has one.</summary>
    public string? Description { get; }

    /// <summary>Added in 1.1: the status id it was created in. Zero on a payload written before it.</summary>
    public int Status { get; }

    public LocalDateRange DateRange { get; }

    /// <summary>
    /// The roles for the initiative. The key is the role type id and the value is an array of employee ids.
    /// </summary>
    public Dictionary<int, Guid[]>? Roles { get; }

    [JsonIgnore]
    public string AggregateType => "StrategicInitiative";
    [JsonIgnore]
    public Guid AggregateId => StrategicInitiativeId;
}
