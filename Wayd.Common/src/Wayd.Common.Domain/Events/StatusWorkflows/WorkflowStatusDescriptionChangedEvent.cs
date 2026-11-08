using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.StatusWorkflows;

/// <summary>
/// A status's description was rewritten, on its own and on any workflow, managed ones included.
/// </summary>
/// <remarks>
/// Separate from <see cref="WorkflowStatusRenamedEvent"/>, which an owned workflow raises for an edit of
/// the name and description together: on a managed workflow the source owns the name, and the description
/// is the part Wayd adds.
/// </remarks>
public sealed record WorkflowStatusDescriptionChangedEvent : DomainEvent<WorkflowStatusDescriptionChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public WorkflowStatusDescriptionChangedEvent(
        Guid id,
        int key,
        Guid statusId,
        string? fromDescription,
        string? toDescription,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        StatusId = statusId;
        FromDescription = fromDescription;
        ToDescription = toDescription;

        Timestamp = timestamp;
    }

    /// <summary>The workflow's id.</summary>
    public Guid Id { get; }

    /// <summary>The workflow's key.</summary>
    public int Key { get; }

    /// <summary>The id of the status whose description changed, which records holding it store.</summary>
    public Guid StatusId { get; }

    /// <summary>The description before the change; null for none.</summary>
    public string? FromDescription { get; }

    /// <summary>The description after the change; null for none.</summary>
    public string? ToDescription { get; }

    [JsonIgnore]
    public string AggregateType => "Workflow";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
