using Wayd.Common.Models;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// Tracking began for a program that existed before <see cref="ProgramCreatedEvent"/> was recorded. Carries that
/// event's payload, describing the program as it stood at <see cref="DomainEvent.Timestamp"/>.
/// </summary>
public sealed record ProgramBaselinedEvent : BaselineEvent<ProgramBaselinedEvent, ProgramCreatedEvent>
{
    public ProgramBaselinedEvent(Guid id, int key, string name, string description, int statusId, LocalDateRange? dateRange, Guid portfolioId, Dictionary<int, Guid[]>? roles, Guid[] strategicThemes, Instant? recordCreatedOn, Guid? recordCreatedById, Instant timestamp)
        : base("Program", id, recordCreatedOn, recordCreatedById, timestamp, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
        StatusId = statusId;
        DateRange = dateRange;
        PortfolioId = portfolioId;
        Roles = roles?.ToDictionary(x => x.Key, x => x.Value.ToArray());
        StrategicThemes = [.. strategicThemes];
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public string Description { get; }
    public int StatusId { get; }
    public LocalDateRange? DateRange { get; }
    public Guid PortfolioId { get; }

    /// <summary>The role type id mapped to the ids of the employees holding it.</summary>
    public Dictionary<int, Guid[]>? Roles { get; }

    public Guid[] StrategicThemes { get; }
}
