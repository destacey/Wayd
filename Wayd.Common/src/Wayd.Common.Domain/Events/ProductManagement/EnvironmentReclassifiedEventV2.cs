using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// An environment's category changed.
/// </summary>
/// <remarks>
/// This looks like ordinary configuration and is not. Marking an environment as production
/// retroactively changes deployment frequency and every measure scoped to production — a number
/// somebody reported last week can move without any deployment having happened. That makes it a fact
/// worth a name and its own event, rather than a field on a generic update.
/// <para>
/// Supersedes <see cref="EnvironmentReclassifiedEvent"/>, dropping its required <c>Name</c>, which described
/// the environment rather than the change. A new type rather than a new version, because removing a required
/// member breaks every consumer written against the old shape.
/// </para>
/// </remarks>
public sealed record EnvironmentReclassifiedEventV2 : DomainEvent, IProductManagementEvent
{
    [JsonConstructor]
    public EnvironmentReclassifiedEventV2(Guid id, int key, EnvironmentCategory fromCategory, EnvironmentCategory toCategory, EventActor actor, Instant timestamp)
        : base(actor, "2.0")
    {
        Id = id;
        Key = key;
        FromCategory = fromCategory;
        ToCategory = toCategory;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public EnvironmentCategory FromCategory { get; }
    public EnvironmentCategory ToCategory { get; }

    [JsonIgnore]
    public string AggregateType => "DeploymentEnvironment";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
