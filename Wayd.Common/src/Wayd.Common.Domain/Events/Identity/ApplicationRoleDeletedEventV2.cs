using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// A role was deleted. Supersedes <see cref="Domain.Identity.ApplicationRoleDeletedEvent"/>, which declared a
/// <c>PermissionsUpdated</c> flag it never set.
/// </summary>
/// <remarks>
/// Carries the name because the role is gone, so the payload is the only remaining description of it.
/// </remarks>
public sealed record ApplicationRoleDeletedEventV2 : DomainEvent<ApplicationRoleDeletedEventV2>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Removed;

    public ApplicationRoleDeletedEventV2(string roleId, string name, EventActor actor, Instant timestamp)
        : base(actor, "2.0")
    {
        RoleId = roleId;
        Name = name;

        Timestamp = timestamp;
    }

    public string RoleId { get; }
    public string Name { get; }

    [JsonIgnore]
    public string AggregateType => "ApplicationRole";
    [JsonIgnore]
    public Guid AggregateId => Guid.Parse(RoleId);
}
