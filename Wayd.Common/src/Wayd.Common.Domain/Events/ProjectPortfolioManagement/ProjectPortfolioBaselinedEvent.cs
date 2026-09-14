using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// Tracking began for a portfolio that existed before <see cref="ProjectPortfolioCreatedEvent"/> was recorded.
/// Carries that event's payload, describing the portfolio as it stood at <see cref="DomainEvent.Timestamp"/>.
/// </summary>
public sealed record ProjectPortfolioBaselinedEvent : BaselineEvent<ProjectPortfolioBaselinedEvent, ProjectPortfolioCreatedEvent>
{
    public ProjectPortfolioBaselinedEvent(Guid id, int key, string name, string description, int statusId, Dictionary<int, Guid[]>? roles, Instant? recordCreatedOn, Guid? recordCreatedById, Instant timestamp)
        : base("ProjectPortfolio", id, recordCreatedOn, recordCreatedById, timestamp, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
        StatusId = statusId;
        Roles = roles?.ToDictionary(x => x.Key, x => x.Value.ToArray());
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public string Description { get; }
    public int StatusId { get; }

    /// <summary>The role type id mapped to the ids of the employees holding it.</summary>
    public Dictionary<int, Guid[]>? Roles { get; }
}
