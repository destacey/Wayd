using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A strategic initiative KPI's name, description, prefix or suffix was edited.
/// </summary>
/// <remarks>
/// Separate from <see cref="StrategicInitiativeKpiTargetChangedEvent"/>, although one form saves both:
/// relabelling a KPI changes how it reads, while moving its target changes what counts as success.
/// </remarks>
public sealed record StrategicInitiativeKpiDetailsUpdatedEvent : DomainEvent<StrategicInitiativeKpiDetailsUpdatedEvent>, IDomainEventDescriptor, IPpmEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public StrategicInitiativeKpiDetailsUpdatedEvent(
        Guid id,
        int key,
        Guid kpiId,
        string name,
        string? description,
        string? prefix,
        string? suffix,
        StrategicInitiativeKpiDetails previous,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        KpiId = kpiId;
        Name = name;
        Description = description;
        Prefix = prefix;
        Suffix = suffix;
        Previous = previous;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid KpiId { get; }
    public string Name { get; }
    public string? Description { get; }
    public string? Prefix { get; }
    public string? Suffix { get; }

    /// <summary>The details this edit replaced.</summary>
    public StrategicInitiativeKpiDetails Previous { get; }

    [JsonIgnore]
    public string AggregateType => "StrategicInitiative";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
