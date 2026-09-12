using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// An environment was retired and can no longer be deployed into.
/// </summary>
/// <remarks>
/// Retired rather than deleted, because historical deployments still point at it and "what was running
/// in production on this date" has to keep resolving after an environment is decommissioned.
/// <para>
/// Supersedes <see cref="EnvironmentRetiredEvent"/>, dropping its required <c>Name</c>, which described the
/// environment rather than the change. A new type rather than a new version, because removing a required
/// member breaks every consumer written against the old shape.
/// </para>
/// </remarks>
public sealed record EnvironmentRetiredEventV2 : DomainEvent, IProductManagementEvent
{
    [JsonConstructor]
    public EnvironmentRetiredEventV2(Guid id, int key, EventActor actor, Instant timestamp)
        : base(actor, "2.0")
    {
        Id = id;
        Key = key;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    [JsonIgnore]
    public string AggregateType => "DeploymentEnvironment";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
