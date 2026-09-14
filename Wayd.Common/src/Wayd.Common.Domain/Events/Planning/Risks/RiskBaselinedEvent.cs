using Wayd.Common.Domain.Enums.Planning;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Risks;

/// <summary>
/// Tracking began for a risk that existed before <see cref="RiskCreatedEvent"/> was recorded. Carries that event's
/// payload, describing the risk as it stood at <see cref="DomainEvent.Timestamp"/>.
/// </summary>
public sealed record RiskBaselinedEvent : BaselineEvent<RiskBaselinedEvent, RiskCreatedEvent>
{
    public RiskBaselinedEvent(
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
        Instant? recordCreatedOn,
        Guid? recordCreatedById,
        Instant timestamp)
        : base("Risk", id, recordCreatedOn, recordCreatedById, timestamp, "1.0")
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
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Summary { get; }
    public string? Description { get; }
    public Guid? TeamId { get; }
    public Instant ReportedOn { get; }
    public Guid ReportedById { get; }
    public RiskStatus Status { get; }
    public RiskCategory Category { get; }
    public RiskGrade Impact { get; }
    public RiskGrade Likelihood { get; }
    public Guid? AssigneeId { get; }
    public LocalDate? FollowUpDate { get; }
    public string? Response { get; }
    public Instant? ClosedDate { get; }
}
