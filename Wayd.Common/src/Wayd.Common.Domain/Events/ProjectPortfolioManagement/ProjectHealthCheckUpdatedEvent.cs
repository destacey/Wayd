using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// An existing health check on a project was corrected.
/// </summary>
/// <remarks>
/// A correction to a record that has already been read and acted on, which is why it is worth
/// distinguishing from the original report rather than restating it.
/// <para>
/// Appended rather than superseding: each check is its own record, so two recorded in one request are two
/// facts. Superseding by event type would also be wrong here for a second reason — it would collapse
/// events about <em>different</em> health checks, which are unrelated.
/// </para>
/// </remarks>
public sealed record ProjectHealthCheckUpdatedEvent : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProjectHealthCheckUpdatedEvent(
        Guid id,
        ProjectKey key,
        string name,
        Guid healthCheckId,
        HealthStatus status,
        string? note,
        Instant expiration,
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

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public ProjectKey Key { get; }
    public string Name { get; }

    /// <summary>The health check this event is about.</summary>
    public Guid HealthCheckId { get; }

    public HealthStatus Status { get; }
    public string? Note { get; }
    public Instant Expiration { get; }

    [JsonIgnore]
    public string AggregateType => "Project";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
