using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Enums.Work;
using NodaTime;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Work.Domain.Models;

public sealed class WorkItemDependency : WorkItemLink
{
    private WorkItemDependency() : base() { }

    private WorkItemDependency(DependencyWorkItemInfo sourceInfo, DependencyWorkItemInfo targetInfo, Instant createdOn, Guid? createdById, Instant? removedOn, Guid? removedById, string? comment)
    : base(sourceInfo.WorkItemId, targetInfo.WorkItemId, WorkItemLinkType.Dependency, createdOn, createdById, removedOn, removedById, comment)
    {
        // assign inputs to properties first (important for state calculation)
        SourceStatusCategory = sourceInfo.StatusCategory;
        SourcePlannedStart = sourceInfo.PlannedStart;
        SourcePlannedOn = sourceInfo.PlannedOn;

        TargetStatusCategory = targetInfo.StatusCategory;
        TargetPlannedStart = targetInfo.PlannedStart;
        TargetPlannedOn = targetInfo.PlannedOn;

        // compute derived values
        CalculateStateAndHealth();
    }

    public StatusCategory SourceStatusCategory { get; set; }

    /// <summary>The first day the source is planned for (see <see cref="DependencyWorkItemInfo.PlannedStart"/>).</summary>
    public LocalDate? SourcePlannedStart { get; set; }

    /// <summary>The last day the source is planned for (see <see cref="DependencyWorkItemInfo.PlannedOn"/>).</summary>
    public LocalDate? SourcePlannedOn { get; set; }

    public StatusCategory TargetStatusCategory { get; set; }

    /// <summary>The first day the target is planned for (see <see cref="DependencyWorkItemInfo.PlannedStart"/>).</summary>
    public LocalDate? TargetPlannedStart { get; set; }

    /// <summary>The last day the target is planned for (see <see cref="DependencyWorkItemInfo.PlannedOn"/>).</summary>
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
        SourcePlannedStart = sourceInfo.PlannedStart;
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
        TargetPlannedStart = targetInfo.PlannedStart;
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

        return new WorkItemDependency(sourceInfo, targetInfo, createdOn, createdById, removedOn, removedById, comment);
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
    /// Calculates the health of the dependency from each end's planned dates and status. Health reads only whether
    /// an end has planned dates, never where they came from. A dependency that has played out is Closed: its
    /// predecessor is done, or removed with a successor that is done or removed too. A removed predecessor with an
    /// open successor stays Unhealthy, because the successor needs work nobody will do. With both ends planned,
    /// their windows are compared by calendar day: the predecessor finishing by the day the successor starts is
    /// Healthy, overlapping it is At Risk, finishing after it is Unhealthy.
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
        var predecessorEnd = SourcePlannedOn;
        var successorEnd = TargetPlannedOn;

        // Nothing waits on a successor with no plan.
        if (successorEnd is null)
        {
            Health = DependencyPlanningHealth.Healthy;
            return;
        }

        // A predecessor with no planned dates, as on a Kanban team, can show no more than that it has started.
        if (predecessorEnd is null)
        {
            Health = SourceStatusCategory == StatusCategory.Active
                ? DependencyPlanningHealth.AtRisk
                : DependencyPlanningHealth.Unhealthy;
            return;
        }

        var successorStart = TargetPlannedStart ?? successorEnd.Value;

        if (predecessorEnd <= successorStart)
            Health = DependencyPlanningHealth.Healthy;
        else if (predecessorEnd <= successorEnd)
            Health = DependencyPlanningHealth.AtRisk;
        else
            Health = DependencyPlanningHealth.Unhealthy;
    }
}
