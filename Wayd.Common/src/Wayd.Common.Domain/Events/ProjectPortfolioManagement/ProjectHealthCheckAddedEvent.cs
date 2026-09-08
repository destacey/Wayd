using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A health check was recorded against a project.
/// </summary>
/// <remarks>
/// The recurring signal on a live project, and the one a watcher is most likely to want told to them:
/// a project turning red is the fact that prompts someone to act.
/// <para>
/// Appended rather than superseding: each check is its own record, so two recorded in one request are two
/// facts. Superseding by event type would also be wrong here for a second reason — it would collapse
/// events about <em>different</em> health checks, which are unrelated.
/// </para>
/// </remarks>
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
        : base(actor)
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
