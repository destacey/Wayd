using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums.Planning;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Risks;

/// <summary>
/// A risk was raised.
/// </summary>
/// <remarks>
/// A risk raised by hand starts Open. An imported one arrives with its status and closed date already known, and
/// records them as its creation.
/// </remarks>
public sealed record RiskCreatedEvent : DomainEvent<RiskCreatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    [JsonConstructor]
    public RiskCreatedEvent(
        Guid id,
        int key,
        string summary,
        string? description,
        Guid? teamId,
        Instant reportedOn,
        Guid reportedById,
        RiskStatus status,
        RiskCategory category,
        RiskGrade impact,
        RiskGrade likelihood,
        Guid? assigneeId,
        LocalDate? followUpDate,
        string? response,
        Instant? closedDate,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Summary = summary;
        Description = description;
        TeamId = teamId;
        ReportedOn = reportedOn;
        ReportedById = reportedById;
        Status = status;
        Category = category;
        Impact = impact;
        Likelihood = likelihood;
        AssigneeId = assigneeId;
        FollowUpDate = followUpDate;
        Response = response;
        ClosedDate = closedDate;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Summary { get; }
    public string? Description { get; }
    public Guid? TeamId { get; }
    public Instant ReportedOn { get; }

    /// <summary>The employee who reported the risk.</summary>
    public Guid ReportedById { get; }

    public RiskStatus Status { get; }
    public RiskCategory Category { get; }
    public RiskGrade Impact { get; }
    public RiskGrade Likelihood { get; }

    /// <summary>The employee the risk is assigned to.</summary>
    public Guid? AssigneeId { get; }

    public LocalDate? FollowUpDate { get; }
    public string? Response { get; }
    public Instant? ClosedDate { get; }

    [JsonIgnore]
    public string AggregateType => "Risk";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
