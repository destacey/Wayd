using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Common.Extensions;

namespace Wayd.ProjectPortfolioManagement.Domain.Models;

/// <summary>
/// Something a project has committed to produce for it to count as done, with the criteria it is accepted
/// against. Part of the <see cref="Project"/> aggregate.
/// </summary>
/// <remarks>
/// Every change goes through the owning project, which decides who may make it, locks a closed project's
/// deliverables, and raises the events; the members here are internal for that reason. The one change that
/// reaches a deliverable directly is a workflow migration (<see cref="StatusTrackedEntity.SwitchWorkflow"/>),
/// which translates its status onto another workflow rather than changing the project's scope.
/// <para>
/// Progress is not stored here: it is derived from linked plan tasks when read, so editing a deliverable never
/// needs the plan loaded.
/// </para>
/// </remarks>
public sealed class ProjectDeliverable : StatusTrackedEntity
{
    private readonly List<ProjectDeliverableTask> _taskLinks = [];

    private ProjectDeliverable() { }

    private ProjectDeliverable(Guid projectId, string name, string? description, string? acceptanceCriteria, int order, Guid? ownerId, LocalDate? targetDate)
    {
        ProjectId = Guard.Against.Default(projectId, nameof(projectId));
        Name = name;
        Description = description;
        AcceptanceCriteria = acceptanceCriteria;
        Order = order;
        OwnerId = ownerId;
        TargetDate = targetDate;
    }

    /// <inheritdoc/>
    public override string StatusOwnerType => PpmWorkflowOwners.ProjectDeliverable.Key;

    /// <summary>The project the deliverable belongs to.</summary>
    public Guid ProjectId { get; private init; }

    /// <summary>What the deliverable is called.</summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>What the deliverable is, in Markdown.</summary>
    public string? Description
    {
        get;
        private set => field = value.NullIfWhiteSpacePlusTrim();
    }

    /// <summary>How the project will know the deliverable is done, in Markdown. What it is accepted against.</summary>
    public string? AcceptanceCriteria
    {
        get;
        private set => field = value.NullIfWhiteSpacePlusTrim();
    }

    /// <summary>The deliverable's position among its project's deliverables. Lower values come first.</summary>
    public int Order { get; private set; }

    /// <summary>The employee accountable for the deliverable, if one is named.</summary>
    public Guid? OwnerId { get; private set; }

    /// <summary>The accountable employee.</summary>
    public Employee? Owner { get; private set; }

    /// <summary>When the deliverable is due, if a date has been committed to.</summary>
    public LocalDate? TargetDate { get; private set; }

    /// <summary>
    /// Why the deliverable is in its current status, when the move needed or was given a reason — always for a
    /// Removed status such as Descoped. Cleared by a move without one.
    /// </summary>
    public string? StatusReason
    {
        get;
        private set => field = value.NullIfWhiteSpacePlusTrim();
    }

    /// <summary>When the deliverable was accepted. Null unless its status means Accepted.</summary>
    public Instant? AcceptedOn { get; private set; }

    /// <summary>
    /// The employee who accepted it, frozen at the time. Null unless its status means Accepted, or when it was
    /// accepted by the system — an import of a finished project.
    /// </summary>
    public Guid? AcceptedById { get; private set; }

    /// <summary>The employee who accepted it.</summary>
    public Employee? AcceptedBy { get; private set; }

    /// <summary>
    /// The plan tasks that serve the deliverable, from which its progress is derived. Setting them
    /// (<see cref="Project.SetDeliverableTasks"/>) and deleting the deliverable need this collection loaded: an
    /// unloaded one reads as no links, so setting would add rows that already exist and a delete would leave rows
    /// that block it.
    /// </summary>
    public IReadOnlyCollection<ProjectDeliverableTask> TaskLinks => _taskLinks.AsReadOnly();

    /// <summary>The well-known meaning of the current status.</summary>
    public ProjectDeliverableStatusAlias StatusAlias => (ProjectDeliverableStatusAlias)StatusAliasValue;

    /// <summary>
    /// Whether the deliverable still has to be delivered or descoped: its status is Proposed or Active. A project
    /// cannot complete while any deliverable is open.
    /// </summary>
    public bool IsOpen => StatusCategory is StatusCategory.Proposed or StatusCategory.Active;

    /// <summary>The descriptive fields, as one value.</summary>
    internal ProjectDeliverableDetails Details => new(Name, Description, AcceptanceCriteria, OwnerId, TargetDate);

    /// <summary>
    /// Whether moving a deliverable into a status needs a reason: any Removed status, such as Descoped, because
    /// dropping a commitment should say why.
    /// </summary>
    public static bool NeedsReason(StatusCategory category) => category == StatusCategory.Removed;

    /// <summary>Replaces the descriptive fields. The caller compares <see cref="Details"/> before and after.</summary>
    internal void UpdateDetails(string name, string? description, string? acceptanceCriteria, Guid? ownerId, LocalDate? targetDate)
    {
        Name = name;
        Description = description;
        AcceptanceCriteria = acceptanceCriteria;
        OwnerId = ownerId;
        TargetDate = targetDate;
    }

    /// <summary>Adds a link to a task. Only the project calls it, having checked the task is its own.</summary>
    internal void AddTaskLink(ProjectDeliverableTask link) => _taskLinks.Add(link);

    /// <summary>Removes a link to a task. Only the project calls it.</summary>
    internal void RemoveTaskLink(ProjectDeliverableTask link) => _taskLinks.Remove(link);

    /// <summary>Sets the deliverable's position. Only the project's reorder calls it.</summary>
    internal void SetOrder(int order) => Order = order;

    /// <summary>
    /// Moves the deliverable to another status of its own workflow, recording the reason and, for Accepted, who
    /// accepted it and when. Refuses before changing anything.
    /// </summary>
    /// <param name="status">A status of the workflow governing the deliverable, resolved by the caller.</param>
    /// <param name="reason">Why; required where <see cref="NeedsReason"/> says so.</param>
    /// <param name="actorEmployeeId">The acting employee, recorded as the acceptor; null for the system.</param>
    internal Result MoveTo(StatusRef status, string? reason, Guid? actorEmployeeId, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(status, nameof(status));

        if (status.WorkflowId != StatusWorkflowId)
            return Result.Failure("That status belongs to a different workflow than the one this deliverable follows. Reload and try again.");

        if (NeedsReason(status.Category) && string.IsNullOrWhiteSpace(reason))
            return Result.Failure($"Moving a deliverable to '{status.Name}' needs a reason.");

        var applied = ApplyStatus(status, actor, timestamp, reason.NullIfWhiteSpacePlusTrim());
        if (applied.IsFailure)
            return applied;

        StatusReason = reason;
        RecordAcceptance(actorEmployeeId, timestamp);

        return Result.Success();
    }

    /// <summary>
    /// Keeps the acceptance in step with a status reached by a workflow migration. Leaving Accepted clears it;
    /// landing on Accepted from another meaning records the switch as the acceptance, with no acceptor, since
    /// nobody accepted it; staying Accepted keeps who accepted it and when.
    /// </summary>
    protected override void OnWorkflowSwitched(Instant timestamp) => RecordAcceptance(actorEmployeeId: null, timestamp);

    /// <summary>
    /// Keeps <see cref="AcceptedOn"/> and <see cref="AcceptedById"/> in step with the status: set on entering
    /// Accepted, cleared on leaving it.
    /// </summary>
    private void RecordAcceptance(Guid? actorEmployeeId, Instant timestamp)
    {
        if (StatusAlias == ProjectDeliverableStatusAlias.Accepted)
        {
            AcceptedOn ??= timestamp;
            AcceptedById ??= actorEmployeeId;
        }
        else
        {
            AcceptedOn = null;
            AcceptedById = null;
        }
    }

    /// <summary>
    /// Creates a deliverable of a project in <paramref name="status"/>. A deliverable created in the ordinary way
    /// starts in Not Started; an import of a finished project creates one already Accepted or Descoped, since a
    /// record's first status is never checked against its workflow's transitions.
    /// </summary>
    /// <param name="status">A status of the workflow governing deliverables, resolved by the caller.</param>
    /// <param name="reason">Why, for a first status that needs one.</param>
    /// <param name="actorEmployeeId">The acting employee, recorded as the acceptor of one created Accepted.</param>
    internal static Result<ProjectDeliverable> Create(
        Guid projectId,
        string name,
        string? description,
        string? acceptanceCriteria,
        int order,
        Guid? ownerId,
        LocalDate? targetDate,
        StatusRef status,
        string? reason,
        Guid? actorEmployeeId,
        EventActor actor,
        Instant timestamp)
    {
        Guard.Against.Null(status, nameof(status));

        if (NeedsReason(status.Category) && string.IsNullOrWhiteSpace(reason))
            return Result.Failure<ProjectDeliverable>($"A deliverable created in '{status.Name}' needs a reason.");

        var deliverable = new ProjectDeliverable(projectId, name, description, acceptanceCriteria, order, ownerId, targetDate);

        var applied = deliverable.ApplyStatus(status, actor, timestamp, reason.NullIfWhiteSpacePlusTrim());
        if (applied.IsFailure)
            return Result.Failure<ProjectDeliverable>(applied.Error);

        deliverable.StatusReason = reason;
        deliverable.RecordAcceptance(actorEmployeeId, timestamp);

        return deliverable;
    }
}
