using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Planning.Risks;
using Wayd.Common.Domain.Interfaces;
using NodaTime;

namespace Wayd.Planning.Domain.Models;

public sealed class Risk : BaseSoftDeletableEntity, IHasIdAndKey
{
    private Risk() { }

    private Risk(string summary, string? description, Guid? teamId, Instant reportedOn, Guid reportedById, RiskCategory category, RiskGrade impact, RiskGrade likelihood, Guid? assigneeId, LocalDate? followUpDate, string? response)
    {
        Summary = summary;
        Description = description;
        TeamId = teamId;
        ReportedOn = reportedOn;
        ReportedById = reportedById;
        Category = category;
        Impact = impact;
        Likelihood = likelihood;
        AssigneeId = assigneeId;
        FollowUpDate = followUpDate;
        Response = response;

        Status = RiskStatus.Open;
    }

    /// <summary>Gets the key.</summary>
    /// <value>The key.</value>
    public int Key { get; private init; }

    /// <summary>
    /// The summary of the Risk.
    /// </summary>
    public string Summary
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Summary)).Trim();
    } = default!;

    /// <summary>
    /// The description of the Risk.
    /// </summary>
    public string? Description
    {
        get;
        private set => field = value.NullIfWhiteSpacePlusTrim();
    }

    // TODO: switch TeamId to ObjectId and Context
    public Guid? TeamId { get; private set; }

    public PlanningTeam? Team { get; private set; }

    public Instant ReportedOn { get; private set; }

    public Guid ReportedById { get; private set; }

    public Employee ReportedBy { get; private set; } = default!;

    public RiskStatus Status { get; private set; }

    public RiskCategory Category { get; private set; }

    public RiskGrade Impact { get; private set; }

    public RiskGrade Likelihood { get; private set; }

    public RiskGrade Exposure => ExposureOf(Impact, Likelihood);

    public Guid? AssigneeId { get; private set; }

    public Employee? Assignee { get; private set; }

    public LocalDate? FollowUpDate { get; private set; }

    public Instant? ClosedDate { get; private set; }

    /// <summary>
    /// What has been done to help prevent the risk from occurring.
    /// </summary>
    public string? Response
    {
        get;
        private set => field = value.NullIfWhiteSpacePlusTrim();
    }

    /// <summary>
    /// Updates an existing risk. Each part that changed raises its own event: the written details, the ROAM
    /// category, the assessment, the assignee, the follow-up date and the status change for different reasons.
    /// </summary>
    public Result Update(string summary, string? description, RiskStatus status, RiskCategory category, RiskGrade impact, RiskGrade likelihood, Guid? assigneeId, LocalDate? followUpDate, string? response, EventActor actor, Instant timestamp)
    {
        try
        {
            var previousDetails = new RiskDetails(Summary, Description, Response);
            var previousCategory = Category;
            var previousImpact = Impact;
            var previousLikelihood = Likelihood;
            var previousAssigneeId = AssigneeId;
            var previousFollowUpDate = FollowUpDate;

            //TeamId isn't updatable at this time
            Summary = summary;
            Description = description;
            Category = category;
            Impact = impact;
            Likelihood = likelihood;
            AssigneeId = assigneeId;
            FollowUpDate = followUpDate;
            Response = response;

            // Compared after assignment because the text setters trim.
            var details = new RiskDetails(Summary, Description, Response);
            if (details != previousDetails)
                AddKeyedDomainEvent(() => new RiskDetailsUpdatedEvent(Id, Key, details.Summary, details.Description, details.Response, previousDetails, actor, timestamp));

            var newCategory = Category;
            if (newCategory != previousCategory)
                AddKeyedDomainEvent(() => new RiskCategoryChangedEvent(Id, Key, previousCategory, newCategory, actor, timestamp));

            var newImpact = Impact;
            var newLikelihood = Likelihood;
            if (newImpact != previousImpact || newLikelihood != previousLikelihood)
                AddKeyedDomainEvent(() => new RiskAssessmentChangedEvent(Id, Key,
                    previousImpact, previousLikelihood, ExposureOf(previousImpact, previousLikelihood),
                    newImpact, newLikelihood, ExposureOf(newImpact, newLikelihood),
                    actor, timestamp));

            var newAssigneeId = AssigneeId;
            if (newAssigneeId != previousAssigneeId)
                AddKeyedDomainEvent(() => new RiskAssigneeChangedEvent(Id, Key, previousAssigneeId, newAssigneeId, actor, timestamp));

            var newFollowUpDate = FollowUpDate;
            if (newFollowUpDate != previousFollowUpDate)
                AddKeyedDomainEvent(() => new RiskFollowUpDateChangedEvent(Id, Key, previousFollowUpDate, newFollowUpDate, actor, timestamp));

            UpdateStatus(status, actor, timestamp);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.ToString());
        }
    }

    private void UpdateStatus(RiskStatus status, EventActor actor, Instant timestamp)
    {
        if (Status == status) return;

        var previousClosedDate = ClosedDate;

        ClosedDate = status == RiskStatus.Closed ? timestamp : null;
        Status = status;

        if (Status == RiskStatus.Closed)
            AddKeyedDomainEvent(() => new RiskClosedEvent(Id, Key, timestamp, actor, timestamp));
        else
            AddKeyedDomainEvent(() => new RiskReopenedEvent(Id, Key, previousClosedDate, actor, timestamp));
    }

    private static RiskGrade ExposureOf(RiskGrade impact, RiskGrade likelihood)
    {
        int exposure = (int)impact + (int)likelihood;
        return exposure switch
        {
            < 4 => RiskGrade.Low,
            4 => RiskGrade.Medium,
            _ => RiskGrade.High,
        };
    }

    /// <summary>
    /// Raises an event whose payload carries <see cref="Key"/>, which the first save assigns; a change made
    /// before it waits for the key. <paramref name="build"/> runs at that point, so capture what it reads.
    /// </summary>
    private void AddKeyedDomainEvent(Func<DomainEvent> build)
    {
        if (Key == 0)
            AddPostPersistenceAction(() => AddDomainEvent(build()));
        else
            AddDomainEvent(build());
    }

    /// <summary>
    /// Raises the creation event once the first save assigns <see cref="Key"/>. Every other value is captured
    /// now, so a caller that changes the risk before that save cannot rewrite the creation.
    /// </summary>
    private void RaiseCreated(EventActor actor, Instant timestamp)
    {
        var summary = Summary;
        var description = Description;
        var teamId = TeamId;
        var reportedOn = ReportedOn;
        var reportedById = ReportedById;
        var status = Status;
        var category = Category;
        var impact = Impact;
        var likelihood = Likelihood;
        var assigneeId = AssigneeId;
        var followUpDate = FollowUpDate;
        var response = Response;
        var closedDate = ClosedDate;

        AddPostPersistenceAction(() => AddDomainEvent(new RiskCreatedEvent(
            Id, Key, summary, description, teamId, reportedOn, reportedById, status, category, impact, likelihood,
            assigneeId, followUpDate, response, closedDate, actor, timestamp)));
    }

    /// <summary>
    /// Create a new risk.
    /// </summary>
    public static Risk Create(string summary, string? description, Guid? teamId, Instant reportedOn, Guid reportedById, RiskCategory category, RiskGrade impact, RiskGrade likelihood, Guid? assigneeId, LocalDate? followUpDate, string? response, EventActor actor, Instant timestamp)
    {
        var risk = new Risk(summary, description, teamId, reportedOn, reportedById, category, impact, likelihood, assigneeId, followUpDate, response);
        risk.RaiseCreated(actor, timestamp);

        return risk;
    }

    /// <summary>
    /// Creates a risk from an external source, with its status and closed date already known.
    /// </summary>
    public static Risk Import(string summary, string? description, Guid? teamId, Instant reportedOn, Guid reportedById, RiskStatus status, RiskCategory category, RiskGrade impact, RiskGrade likelihood, Guid? assigneeId, LocalDate? followUpDate, string? response, Instant? closedDate, EventActor actor, Instant timestamp)
    {
        var risk = new Risk()
        {
            Summary = summary,
            Description = description,
            TeamId = teamId,
            ReportedOn = reportedOn,
            ReportedById = reportedById,
            Status = status,
            Category = category,
            Impact = impact,
            Likelihood = likelihood,
            AssigneeId = assigneeId,
            FollowUpDate = followUpDate,
            Response = response,
            ClosedDate = closedDate,
        };
        risk.RaiseCreated(actor, timestamp);

        return risk;
    }
}
