using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A portfolio was created, in the Proposed status.
/// </summary>
/// <remarks>
/// Raised after persistence, because <see cref="Key"/> is assigned by the database and an event raised at
/// construction would carry zero.
/// </remarks>
public sealed record ProjectPortfolioCreatedEvent : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProjectPortfolioCreatedEvent(
        Guid id,
        int key,
        string name,
        string description,
        int statusId,
        Dictionary<int, Guid[]>? roles,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
        StatusId = statusId;
        Roles = roles?.ToDictionary(x => x.Key, x => x.Value.ToArray());

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public string Description { get; }
    public int StatusId { get; }

    /// <summary>
    /// The roles for the portfolio. The key is the role type id and the value is an array of employee ids.
    /// </summary>
    public Dictionary<int, Guid[]>? Roles { get; }

    [JsonIgnore]
    public string AggregateType => "ProjectPortfolio";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
