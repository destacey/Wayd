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
/// Supersedes <see cref="ProjectHealthCheckUpdatedEvent"/>, dropping its required <c>Name</c>, which
/// described the project rather than the change. A new type rather than a new version, because removing a
/// required member breaks every consumer written against the old shape.
/// </para>
/// </remarks>
public sealed record ProjectHealthCheckUpdatedEventV2 : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProjectHealthCheckUpdatedEventV2(
        Guid id,
        ProjectKey key,
        Guid healthCheckId,
        HealthStatus status,
        string? note,
        Instant expiration,
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

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public ProjectKey Key { get; }

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
