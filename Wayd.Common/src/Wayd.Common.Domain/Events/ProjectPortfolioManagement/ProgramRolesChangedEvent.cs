using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A program's role assignments changed.
/// </summary>
/// <remarks>
/// Role assignment is the path by which delivery leadership itself is granted, so it earns its own event
/// rather than riding on a general update: emptying the Owner and Manager lists can leave a program
/// nobody is authorized to manage, and that must be traceable to whoever did it.
/// <para>
/// Carries both the change and its result. <see cref="Added"/> and <see cref="Removed"/> are the fact —
/// who gained and who lost which role — for a consumer that reacts to it. <see cref="Roles"/> is the
/// roster afterwards, in the encoding <see cref="ProgramCreatedEvent"/> uses, for a consumer that keeps a copy:
/// applying the latest roster is correct however deliveries were ordered or repeated, and applying the
/// deltas is not.
/// </para>
/// </remarks>
public sealed record ProgramRolesChangedEvent : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProgramRolesChangedEvent(
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
    /// Every role assignment on the program after the change. The key is the role type id, the value the
    /// employees holding it.
    /// </summary>
    public Dictionary<int, Guid[]> Roles { get; }

    [JsonIgnore]
    public string AggregateType => "Program";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
