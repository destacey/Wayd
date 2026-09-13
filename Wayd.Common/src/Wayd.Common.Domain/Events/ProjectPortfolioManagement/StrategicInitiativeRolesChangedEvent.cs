using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A strategic initiative's role assignments changed.
/// </summary>
/// <remarks>
/// Carries both the change and its result, as <see cref="ProgramRolesChangedEvent"/> does:
/// <see cref="Added"/> and <see cref="Removed"/> for a consumer reacting to who gained or lost a role, and
/// <see cref="Roles"/>, the roster afterwards in the encoding <see cref="StrategicInitiativeCreatedEvent"/>
/// uses, for a consumer keeping a copy.
/// </remarks>
public sealed record StrategicInitiativeRolesChangedEvent : DomainEvent<StrategicInitiativeRolesChangedEvent>, IDomainEventDescriptor, IPpmEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public StrategicInitiativeRolesChangedEvent(
        Guid id,
        int key,
        RoleAssignmentChange[] added,
        RoleAssignmentChange[] removed,
        Dictionary<int, Guid[]> roles,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Added = [.. added];
        Removed = [.. removed];
        Roles = roles.ToDictionary(x => x.Key, x => x.Value.ToArray());

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The assignments this change granted.</summary>
    public RoleAssignmentChange[] Added { get; }

    /// <summary>The assignments this change took away.</summary>
    public RoleAssignmentChange[] Removed { get; }

    /// <summary>
    /// Every role assignment on the initiative after the change. The key is the role type id, the value the
    /// employees holding it.
    /// </summary>
    public Dictionary<int, Guid[]> Roles { get; }

    [JsonIgnore]
    public string AggregateType => "StrategicInitiative";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
