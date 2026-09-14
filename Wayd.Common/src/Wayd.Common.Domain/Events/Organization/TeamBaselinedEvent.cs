using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Models.Organizations;
using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// Tracking began for a team or team of teams that existed before <see cref="TeamCreatedEvent"/> was recorded.
/// Carries that event's payload, describing the team as it stood at <see cref="DomainEvent.Timestamp"/>.
/// </summary>
public sealed record TeamBaselinedEvent : BaselineEvent<TeamBaselinedEvent, TeamCreatedEvent>
{
    public TeamBaselinedEvent(Guid id, int key, TeamCode code, string name, string? description, TeamType type, LocalDate activeDate, LocalDate? inactiveDate, bool isActive, Instant? recordCreatedOn, Guid? recordCreatedById, Instant timestamp)
        : base("Team", id, recordCreatedOn, recordCreatedById, timestamp, "1.0")
    {
        Id = id;
        Key = key;
        Code = code;
        Name = name;
        Description = description;
        Type = type;
        ActiveDate = activeDate;
        InactiveDate = inactiveDate;
        IsActive = isActive;
    }

    public Guid Id { get; }
    public int Key { get; }
    public TeamCode Code { get; }
    public string Name { get; }
    public string? Description { get; }
    public TeamType Type { get; }
    public LocalDate ActiveDate { get; }
    public LocalDate? InactiveDate { get; }
    public bool IsActive { get; }
}
