using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A health check was removed from a project.
/// </summary>
/// <remarks>
/// Frozen at its published shape and never raised; <see cref="ProjectHealthCheckRemovedEventV2"/> replaced
/// it. Kept so every payload written as this type still deserializes into it — its name and members are the
/// contract those payloads were written against, so neither may change.
/// </remarks>
[Obsolete("Superseded by ProjectHealthCheckRemovedEventV2. Kept only to deserialize payloads already written as this type.")]
public sealed record ProjectHealthCheckRemovedEvent : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProjectHealthCheckRemovedEvent(
        Guid id,
        ProjectKey key,
        string name,
        Guid healthCheckId,
        HealthStatus status,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        HealthCheckId = healthCheckId;
        Status = status;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public ProjectKey Key { get; }
    public string Name { get; }

    /// <summary>The health check this event is about.</summary>
    public Guid HealthCheckId { get; }

    /// <summary>The status the removed check was carrying, so the entry says what was taken away.</summary>
    public HealthStatus Status { get; }

    [JsonIgnore]
    public string AggregateType => "Project";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
