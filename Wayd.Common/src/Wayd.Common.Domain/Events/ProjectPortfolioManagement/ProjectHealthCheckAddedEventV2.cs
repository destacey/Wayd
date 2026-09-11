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
/// Supersedes <see cref="ProjectHealthCheckAddedEvent"/>, dropping its required <c>Name</c>, which described
/// the project rather than the change. A new type rather than a new version, because removing a required
/// member breaks every consumer written against the old shape.
/// </para>
/// </remarks>
public sealed record ProjectHealthCheckAddedEventV2 : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProjectHealthCheckAddedEventV2(
        Guid id,
        ProjectKey key,
        Guid healthCheckId,
        HealthStatus status,
        string? note,
        Instant expiration,
        Guid reportedById,
        EventActor actor,
        Instant timestamp)
        : base(actor, "2.0")
    {
        Id = id;
        Key = key;
        HealthCheckId = healthCheckId;
        Status = status;
        Note = note;
        Expiration = expiration;
        ReportedById = reportedById;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public ProjectKey Key { get; }

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
