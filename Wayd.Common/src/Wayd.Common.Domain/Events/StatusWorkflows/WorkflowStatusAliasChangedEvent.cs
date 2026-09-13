using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.StatusWorkflows;

/// <summary>
/// A status took on, lost or swapped its well-known meaning.
/// </summary>
/// <remarks>
/// Separate from <see cref="WorkflowStatusReclassifiedEvent"/>, although one call changes both: a category
/// change moves what records roll up under, while an alias change moves which status the domain resolves
/// when it needs, say, "Released". Aliases are numbers in the vocabulary of the workflow's owner type, which
/// is fixed at creation.
/// </remarks>
public sealed record WorkflowStatusAliasChangedEvent : DomainEvent<WorkflowStatusAliasChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public WorkflowStatusAliasChangedEvent(
        Guid id,
        int key,
        Guid statusId,
        int fromAlias,
        int toAlias,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        StatusId = statusId;
        FromAlias = fromAlias;
        ToAlias = toAlias;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid StatusId { get; }

    /// <summary>The alias before the change; 0 for none.</summary>
    public int FromAlias { get; }

    /// <summary>The alias after the change; 0 for none.</summary>
    public int ToAlias { get; }

    [JsonIgnore]
    public string AggregateType => "Workflow";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
