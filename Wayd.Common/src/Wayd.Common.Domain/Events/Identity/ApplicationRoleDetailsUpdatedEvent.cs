using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// A role's name or description was edited. Supersedes <see cref="Domain.Identity.ApplicationRoleUpdatedEvent"/> for these edits.
/// </summary>
public sealed record ApplicationRoleDetailsUpdatedEvent : DomainEvent<ApplicationRoleDetailsUpdatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public ApplicationRoleDetailsUpdatedEvent(string roleId, string name, string? description, ApplicationRoleDetails? previous, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        RoleId = roleId;
        Name = name;
        Description = description;
        Previous = previous;

        Timestamp = timestamp;
    }

    public string RoleId { get; }
    public string Name { get; }
    public string? Description { get; }

    /// <summary>
    /// The details this edit replaced. Grouped so that null can only mean "not recorded", since a description
    /// can genuinely be null.
    /// </summary>
    public ApplicationRoleDetails? Previous { get; }

    [JsonIgnore]
    public string AggregateType => "ApplicationRole";
    [JsonIgnore]
    public Guid AggregateId => Guid.Parse(RoleId);
}
