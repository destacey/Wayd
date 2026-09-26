using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// The employee a user is linked to changed: linked, unlinked, or moved to another employee.
/// </summary>
/// <remarks>
/// Both ends are real values, and null on either means the user had, or now has, no employee.
/// </remarks>
public sealed record ApplicationUserEmployeeLinkChangedEvent : DomainEvent<ApplicationUserEmployeeLinkChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public ApplicationUserEmployeeLinkChangedEvent(string userId, Guid? previousEmployeeId, Guid? employeeId, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        UserId = userId;
        PreviousEmployeeId = previousEmployeeId;
        EmployeeId = employeeId;

        Timestamp = timestamp;
    }

    public string UserId { get; }
    public Guid? PreviousEmployeeId { get; }
    public Guid? EmployeeId { get; }

    [JsonIgnore]
    public string AggregateType => "ApplicationUser";
    [JsonIgnore]
    public Guid AggregateId => Guid.Parse(UserId);
}
