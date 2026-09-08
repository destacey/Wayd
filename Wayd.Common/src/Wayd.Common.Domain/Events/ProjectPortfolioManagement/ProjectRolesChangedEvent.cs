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
/// Carries the whole role map after the change, in the shape <see cref="ProjectCreatedEvent"/> uses,
/// rather than a diff. Comparing two entries is how a reader sees who moved.
/// </para>
/// </remarks>
public sealed record ProjectRolesChangedEvent : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProjectRolesChangedEvent(
        Guid id,
        ProjectKey key,
        string name,
        Dictionary<int, Guid[]>? roles,
        EventActor actor,
        Instant timestamp)
        : base(actor)
    {
        Id = id;
        Key = key;
        Name = name;
        Roles = roles?.ToDictionary(x => x.Key, x => x.Value.ToArray());

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public ProjectKey Key { get; }
    public string Name { get; }

    /// <summary>
    /// Every role assignment on the project after the change. The key is the role type id, the value the
    /// employees holding it.
    /// </summary>
    public Dictionary<int, Guid[]>? Roles { get; }

    [JsonIgnore]
    public string AggregateType => "Project";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
