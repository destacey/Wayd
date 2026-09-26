using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Enums.Work;
using NodaTime;

namespace Wayd.Work.Domain.Models;

public sealed class WorkItemDependency : WorkItemLink
{
    private WorkItemDependency() : base() { }

    private WorkItemDependency(Guid sourceId, WorkStatusCategory sourceStatusCategory, LocalDate? sourcePlannedDate, Guid targetId, WorkStatusCategory targetStatusCategory, LocalDate? targetPlannedDate, Instant createdOn, Guid? createdById, Instant? removedOn, Guid? removedById, string? comment, Instant now)
    : base(sourceId, targetId, WorkItemLinkType.Dependency, createdOn, createdById, removedOn, removedById, comment)
    {
        // assign inputs to properties first (important for state calculation)
        SourceStatusCategory = sourceStatusCategory;
        SourcePlannedOn = sourcePlannedDate;

        TargetStatusCategory = targetStatusCategory;
        TargetPlannedOn = targetPlannedDate;

        // compute derived values
        CalculateStateAndHealth(now);
    }

    public WorkStatusCategory SourceStatusCategory { get; set; }

    public LocalDate? SourcePlannedOn { get; set; }

    public WorkStatusCategory TargetStatusCategory { get; set; }

    public LocalDate? TargetPlannedOn { get; set; }

    public DependencyState State { get; private set; }

    public DependencyPlanningHealth Health { get; private set; }

    public Result UpdateSourceAndTargetInfo(DependencyWorkItemInfo sourceInfo, DependencyWorkItemInfo targetInfo, Instant now)
    {
        var sourceResult = UpdateSourceInfo(sourceInfo, now, false);
        if (sourceResult.IsFailure)
            return sourceResult;

        var targetResult = UpdateTargetInfo(targetInfo, now, false);
        if (targetResult.IsFailure)
            return targetResult;

        CalculateStateAndHealth(now);

        return Result.Success();
    }

    /// <summary>
    /// Updates the source information for the current object based on the provided work item details.
    /// </summary>
    /// <remarks>This method updates the source status category and planned date based on the provided 
    /// <paramref name="sourceInfo"/>. It also recalculates derived values such as state and health  using the specified
    /// <paramref name="now"/> timestamp.</remarks>
    /// <param name="sourceInfo">The source work item information containing the updated details.</param>
    /// <param name="now">The current timestamp used to recalculate derived values.</param>
    /// <param name="recalculate">Whether to recalculate state and health. Default is true.</param>
    /// <returns>A <see cref="Result"/> indicating the success or failure of the operation.  Returns a failure result if the
    /// <paramref name="sourceInfo"/> does not match the current source identifier.</returns>
    public Result UpdateSourceInfo(DependencyWorkItemInfo sourceInfo, Instant now, bool recalculate = true)
    {
        Guard.Against.Null(sourceInfo);

        if (sourceInfo.WorkItemId != SourceId)
            return Result.Failure($"Source WorkItemId {sourceInfo.WorkItemId} does not match existing SourceId {SourceId}");

        SourceStatusCategory = sourceInfo.StatusCategory;
        SourcePlannedOn = sourceInfo.PlannedOn;

        // recompute derived values
        if (recalculate)
        {
            CalculateStateAndHealth(now);
        }

        return Result.Success();
    }

    /// <summary>
    /// Updates the target information and recalculates the state and health based on the provided data.
    /// </summary>
    /// <param name="targetInfo">The updated information for the target, including its status category and planned date. Cannot be <see
    /// langword="null"/>.</param>
    /// <param name="now">The current timestamp used for recalculating derived values.</param>
    /// <param name="recalculate">Whether to recalculate state and health. Default is true.</param>
    /// <returns>A <see cref="Result"/> indicating the outcome of the operation. Returns a failure result if the <paramref
    /// name="targetInfo"/> does not match the existing target identifier; otherwise, returns a success result.</returns>
    public Result UpdateTargetInfo(DependencyWorkItemInfo targetInfo, Instant now, bool recalculate = true)
    {
        Guard.Against.Null(targetInfo);

        if (targetInfo.WorkItemId != TargetId)
            return Result.Failure($"Target WorkItemId {targetInfo.WorkItemId} does not match existing TargetId {TargetId}");

        TargetStatusCategory = targetInfo.StatusCategory;
        TargetPlannedOn = targetInfo.PlannedOn;

        // recompute derived values
        if (recalculate)
        {
            CalculateStateAndHealth(now);
        }

        return Result.Success();
    }

    /// <summary>
    /// Calculates both the state and health of the dependency based on current properties and the provided timestamp.
    /// </summary>
    /// <param name="now"></param>
    public void CalculateStateAndHealth(Instant now)
    {
        CalculateState();
        CalculateHealth(now);
    }

    public static WorkItemDependency Create(DependencyWorkItemInfo sourceInfo, DependencyWorkItemInfo targetInfo, Instant createdOn, Guid? createdById, Instant? removedOn, Guid? removedById, string? comment, Instant now)
    {
        Guard.Against.Null(sourceInfo);
        Guard.Against.Null(targetInfo);

        return new WorkItemDependency(sourceInfo.WorkItemId, sourceInfo.StatusCategory, sourceInfo.PlannedOn, targetInfo.WorkItemId, targetInfo.StatusCategory, targetInfo.PlannedOn, createdOn, createdById, removedOn, removedById, comment, now);
    }

    /// <summary>
    /// Calculates the state of the dependency based on the source status category and removal status.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    private void CalculateState()
    {
        if (RemovedOn.HasValue)
        {
            State = DependencyState.Deleted;
            return;
        }

        State = SourceStatusCategory switch
        {
            WorkStatusCategory.Proposed => DependencyState.ToDo,
            WorkStatusCategory.Active => DependencyState.InProgress,
            WorkStatusCategory.Done => DependencyState.Done,
            WorkStatusCategory.Removed => DependencyState.Removed,
            _ => throw new ArgumentOutOfRangeException(nameof(SourceStatusCategory), SourceStatusCategory, null)
        };
    }

    /// <summary>
    /// Calculates the health of the dependency based on planned dates and current state.
    /// </summary>
    /// <param name="now"></param>
    private void CalculateHealth(Instant now)
    {
        if (State == DependencyState.Done)
        {
            Health = DependencyPlanningHealth.Healthy;
            return;
        }

        if (State == DependencyState.Deleted)
        {
            Health = DependencyPlanningHealth.Unknown;
            return;
        }

        if (State == DependencyState.Removed)
        {
            Health = DependencyPlanningHealth.Unhealthy;
            return;
        }

        // if the source is not done but the target is active/done/removed -> unhealthy
        if (TargetStatusCategory is WorkStatusCategory.Active or WorkStatusCategory.Done or WorkStatusCategory.Removed)
        {
            Health = DependencyPlanningHealth.Unhealthy;
            return;
        }

        // A planned date before today (UTC) counts as unplanned. It is a sprint's last day, so that day still counts.
        var today = now.InUtc().Date;
        var predecessor = (SourcePlannedOn.HasValue && SourcePlannedOn.Value < today) ? null : SourcePlannedOn;
        var successor = (TargetPlannedOn.HasValue && TargetPlannedOn.Value < today) ? null : TargetPlannedOn;

        // neither side planned -> at risk
        if (predecessor == null && successor == null)
        {
            Health = DependencyPlanningHealth.AtRisk;
            return;
        }

        // predecessor planned and successor unplanned -> healthy
        if (predecessor != null && successor == null)
        {
            Health = DependencyPlanningHealth.Healthy;
            return;
        }

        // if both planned and predecessor is on or before successor -> healthy
        if (predecessor != null && successor != null && predecessor <= successor)
        {
            Health = DependencyPlanningHealth.Healthy;
            return;
        }

        // otherwise predecessor planned after successor -> unhealthy
        Health = DependencyPlanningHealth.Unhealthy;
    }
}
