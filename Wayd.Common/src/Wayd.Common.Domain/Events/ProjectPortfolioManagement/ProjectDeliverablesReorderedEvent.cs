using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A project's deliverables were put in a new order.
/// </summary>
public sealed record ProjectDeliverablesReorderedEvent : DomainEvent<ProjectDeliverablesReorderedEvent>, IDomainEventDescriptor, IPpmEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ProjectDeliverablesReorderedEvent(
        Guid id,
        ProjectKey key,
        Guid[] previousOrder,
        Guid[] order,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        PreviousOrder = [.. previousOrder];
        Order = [.. order];

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public ProjectKey Key { get; }

    /// <summary>The deliverable ids in their order before the change.</summary>
    public Guid[] PreviousOrder { get; }

    /// <summary>The deliverable ids in their order after the change.</summary>
    public Guid[] Order { get; }

    [JsonIgnore]
    public string AggregateType => "Project";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
