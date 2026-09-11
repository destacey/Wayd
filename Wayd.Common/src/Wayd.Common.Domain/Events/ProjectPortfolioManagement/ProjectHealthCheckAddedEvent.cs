using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A health check was recorded against a project.
/// </summary>
/// <remarks>
/// Frozen at its published shape and never raised; <see cref="ProjectHealthCheckAddedEventV2"/> replaced it.
/// Kept so every payload written as this type still deserializes into it — its name and members are the
/// contract those payloads were written against, so neither may change.
/// </remarks>
[Obsolete("Superseded by ProjectHealthCheckAddedEventV2. Kept only to deserialize payloads already written as this type.")]
public sealed record ProjectHealthCheckAddedEvent : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProjectHealthCheckAddedEvent(
        Guid id,
        ProjectKey key,
        string name,
        Guid healthCheckId,
        HealthStatus status,
        string? note,
        Instant expiration,
        Guid reportedById,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        HealthCheckId = healthCheckId;
        Status = status;
        Note = note;
        Expiration = expiration;
        ReportedById = reportedById;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public ProjectKey Key { get; }
    public string Name { get; }

    /// <summary>The health check this event is about.</summary>
    public Guid HealthCheckId { get; }

    public HealthStatus Status { get; }
    public string? Note { get; }

    /// <summary>When the check stops being current. A project with no unexpired check has no reported health.</summary>
    public Instant Expiration { get; }

    /// <summary>The employee who reported it, frozen here as it is on the check itself.</summary>
    public Guid ReportedById { get; }

    [JsonIgnore]
    public string AggregateType => "Project";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
