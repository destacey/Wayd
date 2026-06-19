using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Interfaces.Organization;
using Wayd.Common.Domain.Models.Organizations;
using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

public sealed record TeamCreatedEvent : DomainEvent, ISimpleTeam
{
    public TeamCreatedEvent(Guid id, int key, TeamCode code, string name, string? description, TeamType type, LocalDate activeDate, LocalDate? inactiveDate, bool isActive, Instant timestamp)
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
        Timestamp = timestamp;
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
