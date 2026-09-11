using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A portfolio's name or description was edited.
/// </summary>
/// <remarks>
/// Carries the details after the change, in the shape <see cref="ProjectPortfolioCreatedEvent"/> uses, so
/// the two compare directly.
/// </remarks>
public sealed record ProjectPortfolioDetailsUpdatedEvent : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProjectPortfolioDetailsUpdatedEvent(
        Guid id,
        int key,
        string name,
        string description,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public string Description { get; }

    [JsonIgnore]
    public string AggregateType => "ProjectPortfolio";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
