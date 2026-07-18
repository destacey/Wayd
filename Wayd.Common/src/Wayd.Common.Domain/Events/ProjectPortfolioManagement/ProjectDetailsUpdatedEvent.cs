using System.Text.Json.Serialization;
using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

public sealed record ProjectDetailsUpdatedEvent : DomainEvent, ISimpleProject
{
    public ProjectDetailsUpdatedEvent(ISimpleProject project, int expenditureCategoryId, Instant timestamp)
        : this(project.Id, project.Key, project.Name, project.Description, expenditureCategoryId, timestamp)
    {
    }

    // Deserialization constructor for the Wolverine durable outbox (STJ binds parameters to properties by
    // name; the primary constructor's `project` parameter cannot be bound).
    [JsonConstructor]
    public ProjectDetailsUpdatedEvent(Guid id, ProjectKey key, string name, string description, int expenditureCategoryId, Instant timestamp)
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
        ExpenditureCategoryId = expenditureCategoryId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public ProjectKey Key { get; }
    public string Name { get; }
    public string Description { get; }
    public int ExpenditureCategoryId { get; }
}