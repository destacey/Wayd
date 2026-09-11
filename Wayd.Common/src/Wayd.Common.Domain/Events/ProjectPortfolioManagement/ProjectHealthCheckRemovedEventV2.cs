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
/// Supersedes <see cref="ProjectHealthCheckRemovedEvent"/>, dropping its required <c>Name</c>, which
/// described the project rather than the change. A new type rather than a new version, because removing a
/// required member breaks every consumer written against the old shape.
/// </para>
/// </remarks>
public sealed record ProjectHealthCheckRemovedEventV2 : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProjectHealthCheckRemovedEventV2(
        Guid id,
        ProjectKey key,
        Guid healthCheckId,
        HealthStatus status,
        EventActor actor,
        Instant timestamp)
        : base(actor, "2.0")
    {
        Id = id;
        Key = key;
        HealthCheckId = healthCheckId;
        Status = status;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public ProjectKey Key { get; }

    /// <summary>The health check this event is about.</summary>
    public Guid HealthCheckId { get; }

    /// <summary>The status the removed check was carrying, so the entry says what was taken away.</summary>
    public HealthStatus Status { get; }

    [JsonIgnore]
    public string AggregateType => "Project";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
