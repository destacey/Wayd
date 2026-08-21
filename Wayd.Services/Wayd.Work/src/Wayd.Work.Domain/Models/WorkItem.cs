using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Work.Domain.Interfaces;
using NodaTime;

namespace Wayd.Work.Domain.Models;

public sealed class WorkItem : BaseAuditableEntity, IHasWorkspace, IHasOptionalWorkTeam
{
    private readonly List<WorkItem> _children = [];
    private readonly List<WorkItemHierarchy> _outboundHierarchyHistory = []; // source links
    private readonly List<WorkItemHierarchy> _inboundHierarchyHistory = []; // target links
    private readonly List<WorkItemDependency> _outboundDependencyHistory = []; // source links
    private readonly List<WorkItemDependency> _inboundDependencyHistory = []; // target links
    private readonly List<WorkItemReference> _referenceLinks = [];
    private readonly List<WorkItemTag> _tags = [];

    //private readonly List<WorkItemRevision> _history = [];

    private WorkItem() { }

    private WorkItem(WorkItemKey key, string title, Guid workspaceId, int? externalId, WorkType workType, int statusId, WorkStatusCategory statusCategory, IWorkItemParentInfo? parentInfo, Guid? teamId, Instant created, Guid? createdById, Instant lastModified, Guid? lastModifiedById, Guid? assignedToId, int? priority, double stackRank, double? storyPoints, Guid? iterationId, Instant? activatedTimestamp, Instant? doneTimestamp, WorkItemExtended? extendedProps, List<WorkItemTag>? tags)
    {
        Key = key;
        Title = title;
        WorkspaceId = workspaceId;
        ExternalId = externalId;
        TypeId = workType.Id;
        StatusId = statusId;
        StatusCategory = statusCategory;
        TeamId = teamId;
        Created = created;
        CreatedById = createdById;
        LastModified = lastModified;
        LastModifiedById = lastModifiedById;
        AssignedToId = assignedToId;
        Priority = priority;
        StackRank = stackRank;
        StoryPoints = storyPoints;
        IterationId = iterationId;
        if (tags != null) _tags.AddRange(tags.Distinct());

        ActivatedTimestamp = activatedTimestamp;
        DoneTimestamp = doneTimestamp;
        // Rebound to this item's id: the caller builds the row before the id exists.
        ExtendedProps = extendedProps is null
            ? null
            : WorkItemExtended.Create(
                Id,
                extendedProps.ExternalTeamIdentifier,
                extendedProps.AssignedToExternalId,
                extendedProps.CreatedByExternalId,
                extendedProps.LastModifiedByExternalId);

        var result = UpdateParent(parentInfo, workType);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error);
        }
    }

    /// <summary>A unique key to identify the work item.</summary>
    /// <value>The key.</value>
    public WorkItemKey Key
    {
        get;
        private set => field = Guard.Against.Null(value, nameof(Key));
    } = null!;

    public string Title
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Title)).Trim();

    } = null!;

    public Guid WorkspaceId { get; private set; }

    public Workspace Workspace { get; private set; } = null!;

    public int? ExternalId { get; private init; }

    public int TypeId { get; private set; }

    public WorkType Type { get; private set; } = null!;

    public int StatusId { get; private set; }

    public WorkStatus Status { get; private set; } = null!;

    public WorkStatusCategory StatusCategory { get; private set; }

    public Guid? ParentId { get; private set; }

    public WorkItem? Parent { get; private set; }

    public IReadOnlyCollection<WorkItem> Children => _children.AsReadOnly();

    public Guid? TeamId { get; private set; }

    public WorkTeam? Team { get; private set; }

    public Guid? AssignedToId { get; private set; }

    public Employee? AssignedTo { get; private set; }

    public Instant Created { get; private set; }

    public Guid? CreatedById { get; private set; }

    public Employee? CreatedBy { get; private set; }

    public Instant LastModified { get; private set; }

    public Guid? LastModifiedById { get; private set; }

    public Employee? LastModifiedBy { get; private set; }

    public int? Priority { get; private set; }

    // TODO: other systems will use different types.  How to handle this?
    public double StackRank { get; private set; }

    public double? StoryPoints { get; private set; }

    /// <summary>
    /// The overriding project id of the work item.  It is used to override the project id coming from the parent work item.
    /// </summary>
    public Guid? ProjectId { get; private set; }

    public WorkProject? Project { get; private set; }

    public Guid? ParentProjectId { get; private set; }

    public WorkProject? ParentProject { get; private set; }

    /// <summary>
    /// Returns the effective project for this work item, with priority given to direct assignment.
    /// Uses this work item's ProjectId if set, otherwise falls back to the ParentProjectId inheritance.
    /// </summary>
    public Guid? CurrentProjectId => ProjectId ?? ParentProjectId;

    /// <summary>
    /// The current iteration id of the work item.
    /// </summary>
    public Guid? IterationId { get; set; }

    /// <summary>
    /// The current iteration of the work item.
    /// </summary>
    public WorkIteration? Iteration { get; set; }

    public Instant? ActivatedTimestamp { get; private set; }

    public Instant? DoneTimestamp { get; private set; }

    public IReadOnlyCollection<WorkItemTag> Tags => _tags.AsReadOnly();

    public WorkItemExtended? ExtendedProps { get; private set; }

    public IReadOnlyCollection<WorkItemDependency> OutboundDependencies => _outboundDependencyHistory.AsReadOnly();
    public IReadOnlyCollection<WorkItemDependency> InboundDependencies => _inboundDependencyHistory.AsReadOnly();
    public IReadOnlyCollection<WorkItemHierarchy> OutboundHierarchies => _outboundHierarchyHistory.AsReadOnly();
    public IReadOnlyCollection<WorkItemHierarchy> InboundHierarchies => _inboundHierarchyHistory.AsReadOnly();

    public IReadOnlyCollection<WorkItemReference> ReferenceLinks => _referenceLinks.AsReadOnly();

    /// <summary>
    /// The collection of revisions for the life of the work item.
    /// </summary>
    //public IReadOnlyCollection<WorkItemRevision> History => _history.AsReadOnly();

    /// <summary>
    /// Updates the work item properties.
    /// </summary>
    /// <param name="title"></param>
    /// <param name="workType"></param>
    /// <param name="statusId"></param>
    /// <param name="statusCategory"></param>
    /// <param name="parentInfo"></param>
    /// <param name="teamId"></param>
    /// <param name="lastModified"></param>
    /// <param name="lastModifiedById"></param>
    /// <param name="assignedToId"></param>
    /// <param name="priority"></param>
    /// <param name="stackRank"></param>
    /// <param name="storyPoints"></param>
    /// <param name="iterationId"></param>
    /// <param name="activatedTimestamp"></param>
    /// <param name="doneTimestamp"></param>
    /// <param name="extendedProps"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void Update(string title, WorkType workType, int statusId, WorkStatusCategory statusCategory, IWorkItemParentInfo? parentInfo, Guid? teamId, Instant lastModified, Guid? lastModifiedById, Guid? assignedToId, int? priority, double stackRank, double? storyPoints, Guid? iterationId, Instant? activatedTimestamp, Instant? doneTimestamp, WorkItemExtended? extendedProps, List<WorkItemTag>? tags = null)
    {
        if (extendedProps != null && Id != extendedProps.Id)
        {
            throw new InvalidOperationException("The extended properties must match the work item.");
        }

        SetActivatedTimestamp(activatedTimestamp, Created, doneTimestamp);
        SetDoneTimestamp(doneTimestamp, Created, statusCategory);

        Title = title;
        TypeId = workType.Id;
        StatusId = statusId;
        StatusCategory = statusCategory;
        TeamId = teamId;
        LastModified = lastModified;
        LastModifiedById = lastModifiedById;
        AssignedToId = assignedToId;
        Priority = priority;
        StackRank = stackRank;
        StoryPoints = storyPoints;
        IterationId = iterationId;
        _tags.Clear();
        if (tags != null) _tags.AddRange(tags.Distinct());

        var result = UpdateParent(parentInfo, workType);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error);
        }

        if (extendedProps != null)
        {
            ExtendedProps = ExtendedProps ?? extendedProps;
            ExtendedProps.Update(extendedProps);
        }
        else
        {
            ExtendedProps = null;
        }
    }

    // TODO: Running into EF issues when set the WorkType directly. So adding it for the check.
    public Result UpdateParent(IWorkItemParentInfo? parentInfo, WorkType currentWorkType)
    {
        if (currentWorkType?.Level?.Tier is null)
        {
            return Result.Failure("Unable to set the work item parent without the type and level.");
        }

        if (parentInfo is not null)
        {
            if (parentInfo.Tier != WorkTypeTier.Portfolio)
            {
                return Result.Failure("Only portfolio tier work items can be parents.");
            }

            if (currentWorkType.Level.Tier == WorkTypeTier.Portfolio && currentWorkType.Level.Order <= parentInfo.LevelOrder)
            {
                return Result.Failure("The parent must be a higher level than the work item.");
            }
        }

        ParentId = parentInfo?.Id;

        // Project Ids are not set for WorkTypes in the Other tier
        if (currentWorkType.Level.Tier is not WorkTypeTier.Other)
        {
            ParentProjectId = parentInfo?.ProjectId;

            TryResetProjectId();
        }

        return Result.Success();
    }

    public Result AddSuccessorLink(DependencyWorkItemInfo targetInfo, Instant createdOn, Guid? createdById, string? comment, Instant now)
    {
        if (Id == targetInfo.WorkItemId)
        {
            return Result.Failure("A work item cannot be linked to itself.");
        }

        var sourceInfo = DependencyWorkItemInfo.Create(this);

        var existingLink = _outboundDependencyHistory.FirstOrDefault(x => x.TargetId == targetInfo.WorkItemId
            && x.CreatedOn == createdOn);
        if (existingLink is not null)
        {
            // TODO: move update successor link to it's own method
            existingLink.Update(createdById, existingLink.RemovedById, comment);
            existingLink.UpdateSourceAndTargetInfo(sourceInfo, targetInfo, now);
        }
        else
        {
            var newLink = WorkItemDependency.Create(sourceInfo, targetInfo, createdOn, createdById, null, null, comment, now);
            _outboundDependencyHistory.Add(newLink);
        }

        return Result.Success();
    }

    public Result RemoveSuccessorLink(Guid targetId, Instant timestamp, Guid? removedById)
    {
        if (Id == targetId)
        {
            return Result.Failure("A work item cannot be linked to itself.");
        }

        var existingLink = _outboundDependencyHistory.FirstOrDefault(x => x.TargetId == targetId
            && x.RemovedOn == timestamp);
        if (existingLink is not null)
        {
            existingLink.Update(existingLink.CreatedById, removedById, existingLink.Comment);
        }
        else
        {
            var link = _outboundDependencyHistory.OrderBy(l => l.CreatedOn).FirstOrDefault(x => x.TargetId == targetId
                && x.RemovedOn is null
                && x.CreatedOn < timestamp);
            if (link is null)
            {
                return Result.Failure("The successor link does not exist.");
            }

            link.RemoveLink(timestamp, removedById);
        }

        return Result.Success();
    }

    /// <summary>
    /// Updates the project id of the work item.  If the project id is the same as the parent project id, it will be set to null.
    /// </summary>
    /// <param name="projectId"></param>
    /// <returns></returns>
    public Result UpdateProjectId(Guid? projectId)
    {
        Guard.Against.Null(Type?.Level, nameof(Type.Level));

        if (Type.Level.Tier is not WorkTypeTier.Portfolio)
        {
            return Result.Failure("Only portfolio tier work items can have a project id.");
        }

        ProjectId = projectId;

        TryResetProjectId();

        return Result.Success();
    }

    /// <summary>
    /// Changes the workspace of the work item.
    /// </summary>
    /// <param name="workspaceId"></param>
    /// <param name="workType"></param>
    /// <param name="statusId"></param>
    /// <param name="statusCategory"></param>
    public Result ChangeExternalWorkspace(Workspace workspace, WorkType workType, int statusId, WorkStatusCategory statusCategory)
    {
        Guard.Against.Null(workspace, nameof(workspace));
        Guard.Against.Null(workType, nameof(workType));

        if (workspace.OwnershipInfo.Ownership is not Ownership.Managed)
        {
            return Result.Failure("Only managed workspaces can have external work items.");
        }

        if (ExternalId is null)
        {
            return Result.Failure("Only external work items can change workspaces.");
        }

        var key = new WorkItemKey(workspace.Key, ExternalId.Value);

        Key = key;
        WorkspaceId = workspace.Id;
        TypeId = workType.Id;
        StatusId = statusId;
        StatusCategory = statusCategory;

        return Result.Success();
    }

    public static WorkItem CreateExternal(Workspace workspace, int externalId, string title, WorkType workType, int statusId, WorkStatusCategory statusCategory, IWorkItemParentInfo? parentInfo, Guid? teamId, Instant created, Guid? createdById, Instant lastModified, Guid? lastModifiedById, Guid? assignedToId, int? priority, double stackRank, double? storyPoints, Guid? iterationId, Instant? activatedTimestamp, Instant? doneTimestamp, WorkItemExtended? extendedProps, List<WorkItemTag>? tags = null)
    {
        Guard.Against.Null(workspace, nameof(workspace));
        Guard.Against.Null(workType, nameof(workType));

        if (workspace.OwnershipInfo.Ownership is not Ownership.Managed)
        {
            throw new InvalidOperationException("Only managed workspaces can have external work items.");
        }

        var key = new WorkItemKey(workspace.Key, externalId);
        return new WorkItem(key, title, workspace.Id, externalId, workType, statusId, statusCategory, parentInfo, teamId, created, createdById, lastModified, lastModifiedById, assignedToId, priority, stackRank, storyPoints, iterationId, activatedTimestamp, doneTimestamp, extendedProps, tags);

        //var result = workspace.AddWorkItem(workItem);  // this is handled in the handler for performance reasons
    }

    /// <summary>
    /// Resets the project id if the parent project id is the same as the work item project id.
    /// </summary>
    private void TryResetProjectId()
    {
        if (ProjectId.HasValue && ProjectId == ParentProjectId)
        {
            ProjectId = null;
        }
    }

    private void SetActivatedTimestamp(Instant? activatedTimestamp, Instant created, Instant? doneTimestamp)
    {
        if (activatedTimestamp.HasValue && activatedTimestamp.Value < created)
        {
            throw new InvalidOperationException("The activated timestamp cannot be before the created timestamp.");
        }

        if (activatedTimestamp is null && doneTimestamp.HasValue)
        {
            ActivatedTimestamp = doneTimestamp;
            return;
        }

        // If the activated timestamp is after the done timestamp, set the activated timestamp to the done timestamp
        if (activatedTimestamp.HasValue && doneTimestamp.HasValue && doneTimestamp < activatedTimestamp)
        {
            ActivatedTimestamp = doneTimestamp;
            return;
        }

        ActivatedTimestamp = activatedTimestamp;
    }

    private void SetDoneTimestamp(Instant? doneTimestamp, Instant created, WorkStatusCategory statusCategory)
    {
        if (doneTimestamp.HasValue && doneTimestamp < created)
        {
            throw new InvalidOperationException("The completed timestamp cannot be before the created timestamp.");
        }

        DoneTimestamp = doneTimestamp.HasValue && (statusCategory is WorkStatusCategory.Done or WorkStatusCategory.Removed) ? doneTimestamp : null;
    }
}
