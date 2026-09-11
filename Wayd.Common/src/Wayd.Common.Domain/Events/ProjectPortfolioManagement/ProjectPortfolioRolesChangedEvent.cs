using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A portfolio's role assignments changed.
/// </summary>
/// <remarks>
/// Role assignment is the path by which delivery leadership itself is granted, and a portfolio has no
/// ancestor to inherit it from: emptying its Owner and Manager lists leaves a portfolio — and everything
/// under it — that only a PPM administrator can manage. That has to be traceable to whoever did it.
/// <para>
/// Carries both the change and its result. <see cref="Added"/> and <see cref="Removed"/> are the fact —
/// who gained and who lost which role — for a consumer that reacts to it. <see cref="Roles"/> is the
/// roster afterwards, in the encoding <see cref="ProjectPortfolioCreatedEvent"/> uses, for a consumer that keeps a copy:
/// applying the latest roster is correct however deliveries were ordered or repeated, and applying the
/// deltas is not.
/// </para>
/// </remarks>
public sealed record ProjectPortfolioRolesChangedEvent : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProjectPortfolioRolesChangedEvent(
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
    /// Every role assignment on the portfolio after the change. The key is the role type id, the value the
    /// employees holding it.
    /// </summary>
    public Dictionary<int, Guid[]> Roles { get; }

    [JsonIgnore]
    public string AggregateType => "ProjectPortfolio";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
