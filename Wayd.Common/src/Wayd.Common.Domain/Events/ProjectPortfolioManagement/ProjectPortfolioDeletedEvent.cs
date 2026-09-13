using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A portfolio was deleted.
/// </summary>
/// <remarks>
/// The name is carried because the row it describes is gone by the time anyone reads the entry.
/// </remarks>
public sealed record ProjectPortfolioDeletedEvent : DomainEvent<ProjectPortfolioDeletedEvent>, IDomainEventDescriptor, IPpmEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Removed;

    [JsonConstructor]
    public ProjectPortfolioDeletedEvent(
        Guid id,
        int key,
        string name,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }

    [JsonIgnore]
    public string AggregateType => "ProjectPortfolio";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
