using System.Text.Json.Serialization;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A project's role assignments changed.
/// </summary>
/// <remarks>
/// Role assignment is the path by which delivery leadership itself is granted, so it earns its own event
/// rather than riding on a general update: emptying the Owner and Manager lists can leave a project
/// nobody is authorized to manage, and that must be traceable to whoever did it.
/// <para>
/// Carries both the change and its result. <see cref="Added"/> and <see cref="Removed"/> are the fact —
/// who gained and who lost which role — for a consumer that reacts to it. <see cref="Roles"/> is the
/// roster afterwards, in the encoding <see cref="ProjectCreatedEvent"/> uses, for a consumer that keeps a copy:
/// applying the latest roster is correct however deliveries were ordered or repeated, and applying the
/// deltas is not.
/// </para>
/// <para>
/// Supersedes <see cref="ProjectRolesChangedEvent"/>, dropping its required <c>Name</c>, which described the
/// project rather than the change. A new type rather than a new version, because removing a required member
/// breaks every consumer written against the old shape.
/// </para>
/// </remarks>
public sealed record ProjectRolesChangedEventV2 : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProjectRolesChangedEventV2(
        Guid id,
        ProjectKey key,
        RoleAssignmentChange[] added,
        RoleAssignmentChange[] removed,
        Dictionary<int, Guid[]> roles,
        EventActor actor,
        Instant timestamp)
        : base(actor, "2.0")
    {
        Id = id;
        Key = key;
        Added = [.. added];
        Removed = [.. removed];
        Roles = roles.ToDictionary(x => x.Key, x => x.Value.ToArray());

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public ProjectKey Key { get; }

    /// <summary>The assignments this change granted.</summary>
    public RoleAssignmentChange[] Added { get; }

    /// <summary>The assignments this change took away.</summary>
    public RoleAssignmentChange[] Removed { get; }

    /// <summary>
    /// Every role assignment on the project after the change. The key is the role type id, the value the
    /// employees holding it.
    /// </summary>
    public Dictionary<int, Guid[]> Roles { get; }

    [JsonIgnore]
    public string AggregateType => "Project";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
