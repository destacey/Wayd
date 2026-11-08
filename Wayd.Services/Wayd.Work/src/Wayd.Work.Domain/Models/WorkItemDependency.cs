using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Enums.Work;
using NodaTime;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Work.Domain.Models;

public sealed class WorkItemDependency : WorkItemLink
{
    private WorkItemDependency() : base() { }

    private WorkItemDependency(Guid sourceId, StatusCategory sourceStatusCategory, LocalDate? sourcePlannedDate, Guid targetId, StatusCategory targetStatusCategory, LocalDate? targetPlannedDate, Instant createdOn, Guid? createdById, Instant? removedOn, Guid? removedById, string? comment)
    : base(sourceId, targetId, WorkItemLinkType.Dependency, createdOn, createdById, removedOn, removedById, comment)
    {
        // assign inputs to properties first (important for state calculation)
        SourceStatusCategory = sourceStatusCategory;
        SourcePlannedOn = sourcePlannedDate;

        TargetStatusCategory = targetStatusCategory;
        TargetPlannedOn = targetPlannedDate;

        // compute derived values
        CalculateStateAndHealth();
    }

    public StatusCategory SourceStatusCategory { get; set; }

    /// <summary>The source's plan: the last planned day of its sprint, while that sprint has not ended.</summary>
    public LocalDate? SourcePlannedOn { get; set; }

    public StatusCategory TargetStatusCategory { get; set; }

    /// <summary>The target's plan: the last planned day of its sprint, while that sprint has not ended.</summary>
    public LocalDate? TargetPlannedOn { get; set; }

    public DependencyState State { get; private set; }

    public DependencyPlanningHealth Health { get; private set; }

    public Result UpdateSourceAndTargetInfo(DependencyWorkItemInfo sourceInfo, DependencyWorkItemInfo targetInfo)
    {
        var sourceResult = UpdateSourceInfo(sourceInfo, false);
        if (sourceResult.IsFailure)
            return sourceResult;

        var targetResult = UpdateTargetInfo(targetInfo, false);
        if (targetResult.IsFailure)
            return targetResult;

        CalculateStateAndHealth();

        return Result.Success();
    }

    /// <summary>
    /// Updates the source information for the current object based on the provided work item details.
    /// </summary>
    /// <remarks>This method updates the source status category and planned date based on the provided
    /// <paramref name="sourceInfo"/>, and recalculates derived values such as state and health.</remarks>
    /// <param name="sourceInfo">The source work item information containing the updated details.</param>
    /// <param name="recalculate">Whether to recalculate state and health. Default is true.</param>
    /// <returns>A <see cref="Result"/> indicating the success or failure of the operation.  Returns a failure result if the
    /// <paramref name="sourceInfo"/> does not match the current source identifier.</returns>
    public Result UpdateSourceInfo(DependencyWorkItemInfo sourceInfo, bool recalculate = true)
    {
        Guard.Against.Null(sourceInfo);

        if (sourceInfo.WorkItemId != SourceId)
            return Result.Failure($"Source WorkItemId {sourceInfo.WorkItemId} does not match existing SourceId {SourceId}");

        SourceStatusCategory = sourceInfo.StatusCategory;
        SourcePlannedOn = sourceInfo.PlannedOn;

        // recompute derived values
        if (recalculate)
        {
            CalculateStateAndHealth();
        }

        return Result.Success();
    }

    /// <summary>
    /// Updates the target information and recalculates the state and health based on the provided data.
    /// </summary>
    /// <param name="targetInfo">The updated information for the target, including its status category and planned date. Cannot be <see
    /// langword="null"/>.</param>
    /// <param name="recalculate">Whether to recalculate state and health. Default is true.</param>
    /// <returns>A <see cref="Result"/> indicating the outcome of the operation. Returns a failure result if the <paramref
    /// name="targetInfo"/> does not match the existing target identifier; otherwise, returns a success result.</returns>
    public Result UpdateTargetInfo(DependencyWorkItemInfo targetInfo, bool recalculate = true)
    {
        Guard.Against.Null(targetInfo);

        if (targetInfo.WorkItemId != TargetId)
            return Result.Failure($"Target WorkItemId {targetInfo.WorkItemId} does not match existing TargetId {TargetId}");

        TargetStatusCategory = targetInfo.StatusCategory;
        TargetPlannedOn = targetInfo.PlannedOn;

        // recompute derived values
        if (recalculate)
        {
            CalculateStateAndHealth();
        }

        return Result.Success();
    }

    /// <summary>
    /// Calculates both the state and health of the dependency from its current properties.
    /// </summary>
    public void CalculateStateAndHealth()
    {
        CalculateState();
        CalculateHealth();
    }

    public static WorkItemDependency Create(DependencyWorkItemInfo sourceInfo, DependencyWorkItemInfo targetInfo, Instant createdOn, Guid? createdById, Instant? removedOn, Guid? removedById, string? comment)
    {
        Guard.Against.Null(sourceInfo);
        Guard.Against.Null(targetInfo);

        return new WorkItemDependency(sourceInfo.WorkItemId, sourceInfo.StatusCategory, sourceInfo.PlannedOn, targetInfo.WorkItemId, targetInfo.StatusCategory, targetInfo.PlannedOn, createdOn, createdById, removedOn, removedById, comment);
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
            StatusCategory.Proposed => DependencyState.ToDo,
            StatusCategory.Active => DependencyState.InProgress,
            StatusCategory.Done => DependencyState.Done,
            StatusCategory.Removed => DependencyState.Removed,
            _ => throw new ArgumentOutOfRangeException(nameof(SourceStatusCategory), SourceStatusCategory, null)
        };
    }

    /// <summary>
    /// Calculates the health of the dependency based on planned dates and current state. A dependency that has
    /// played out is Closed: its predecessor is done, or removed with a successor that is done or removed too.
    /// A removed predecessor with an open successor stays Unhealthy, because the successor needs work nobody
    /// will do.
    /// </summary>
    private void CalculateHealth()
    {
        // No view shows a deleted link, so its ends' statuses are not read.
        if (State == DependencyState.Deleted)
        {
            Health = DependencyPlanningHealth.Unknown;
            return;
        }

        if (State == DependencyState.Done)
        {
            Health = DependencyPlanningHealth.Closed;
            return;
        }

        if (State == DependencyState.Removed)
        {
            Health = TargetStatusCategory is StatusCategory.Done or StatusCategory.Removed
                ? DependencyPlanningHealth.Closed
                : DependencyPlanningHealth.Unhealthy;
            return;
        }

        // if the source is not done but the target is active/done/removed -> unhealthy
        if (TargetStatusCategory is StatusCategory.Active or StatusCategory.Done or StatusCategory.Removed)
        {
            Health = DependencyPlanningHealth.Unhealthy;
            return;
        }

        // An end whose sprint has ended carries no planned date (see DependencyWorkItemInfo.PlannedOn).
        var predecessor = SourcePlannedOn;
        var successor = TargetPlannedOn;

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
