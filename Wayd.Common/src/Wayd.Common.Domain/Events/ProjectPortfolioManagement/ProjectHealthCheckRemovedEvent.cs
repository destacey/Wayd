using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A health check was removed from a project.
/// </summary>
/// <remarks>
/// Removing a check can change the project's reported health, by uncovering an older one or leaving none
/// at all, so it is a fact in its own right rather than the mere absence of one.
/// <para>
/// Appended rather than superseding: each check is its own record, so two recorded in one request are two
/// facts. Superseding by event type would also be wrong here for a second reason — it would collapse
/// events about <em>different</em> health checks, which are unrelated.
/// </para>
/// </remarks>
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
        : base(actor)
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
