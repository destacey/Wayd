using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A deliverable was added to a project.
/// </summary>
/// <remarks>
/// The deliverable is part of the project's aggregate, so the entry belongs to the project and carries the
/// deliverable as it was added, including the status it started in — an import adds a deliverable of a
/// finished project already Accepted.
/// </remarks>
public sealed record ProjectDeliverableAddedEvent : DomainEvent<ProjectDeliverableAddedEvent>, IDomainEventDescriptor, IPpmEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ProjectDeliverableAddedEvent(
        Guid id,
        ProjectKey key,
        Guid deliverableId,
        string name,
        string? description,
        string? acceptanceCriteria,
        int order,
        Guid? ownerId,
        LocalDate? targetDate,
        Guid statusId,
        string statusName,
        StatusCategory statusCategory,
        ProjectDeliverableStatusAlias statusAlias,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        DeliverableId = deliverableId;
        Name = name;
        Description = description;
        AcceptanceCriteria = acceptanceCriteria;
        Order = order;
        OwnerId = ownerId;
        TargetDate = targetDate;
        StatusId = statusId;
        StatusName = statusName;
        StatusCategory = statusCategory;
        StatusAlias = statusAlias;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public ProjectKey Key { get; }

    /// <summary>The deliverable this event is about.</summary>
    public Guid DeliverableId { get; }

    public string Name { get; }
    public string? Description { get; }
    public string? AcceptanceCriteria { get; }
    public int Order { get; }

    /// <summary>The employee accountable for the deliverable, if one was named.</summary>
    public Guid? OwnerId { get; }

    public LocalDate? TargetDate { get; }
    public Guid StatusId { get; }
    public string StatusName { get; }
    public StatusCategory StatusCategory { get; }
    public ProjectDeliverableStatusAlias StatusAlias { get; }

    [JsonIgnore]
    public string AggregateType => "Project";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
