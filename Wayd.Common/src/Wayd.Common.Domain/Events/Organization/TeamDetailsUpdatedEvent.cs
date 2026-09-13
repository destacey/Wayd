using System.Text.Json.Serialization;
using Wayd.Common.Domain.Models.Organizations;
using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// A team's or team of teams' code, name or description was edited. Supersedes <see cref="TeamUpdatedEvent"/>.
/// </summary>
public sealed record TeamDetailsUpdatedEvent : DomainEvent, IAggregateEvent
{
    public TeamDetailsUpdatedEvent(Guid id, int key, TeamCode code, string name, string? description, TeamDetails? previous, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Code = code;
        Name = name;
        Description = description;
        Previous = previous;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public TeamCode Code { get; }
    public string Name { get; }
    public string? Description { get; }

    /// <summary>
    /// The details this edit replaced. Grouped so that null can only mean "not recorded", as
    /// <c>ProjectDetailsUpdatedEvent.Previous</c> is.
    /// </summary>
    public TeamDetails? Previous { get; }

    [JsonIgnore]
    public string AggregateType => "Team";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
