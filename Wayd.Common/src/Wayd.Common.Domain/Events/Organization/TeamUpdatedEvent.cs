using Wayd.Common.Domain.Models.Organizations;
using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

public sealed record TeamUpdatedEvent : DomainEvent
{
    public TeamUpdatedEvent(Guid id, TeamCode code, string name, string? description, Instant timestamp)
    {
        Id = id;
        Code = code;
        Name = name;
        Description = description;
        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public TeamCode Code { get; }
    public string Name { get; }
    public string? Description { get; }
}
