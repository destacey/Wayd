using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Enums.Work;
using NodaTime;

namespace Wayd.Work.Domain.Models;

public sealed class WorkflowScheme : BaseSoftDeletableEntity, IActivatable
{
    private WorkflowScheme() { }
    internal WorkflowScheme(Guid workflowId, int workStatusId, WorkStatusCategory workStatusCategory, int order)
    {
        WorkflowId = workflowId;
        WorkStatusId = workStatusId;
        WorkStatusCategory = workStatusCategory;
        Order = order;
    }
    internal WorkflowScheme(Workflow workflow, int workStatusId, WorkStatusCategory workStatusCategory, int order, bool isActive)
    {
        Workflow = workflow;
        WorkStatusId = workStatusId;
        WorkStatusCategory = workStatusCategory;
        Order = order;
        IsActive = isActive;
    }

    public Guid WorkflowId { get; }
    public Workflow? Workflow { get; private set; }
    public int WorkStatusId { get; }
    public WorkStatus? WorkStatus { get; private set; }
    public WorkStatusCategory WorkStatusCategory { get; private set; }
    public int Order { get; private set; }

    public bool IsActive { get; private set; } = true;

    public Result Activate(Instant timestamp)
    {
        if (!IsActive)
        {
            IsActive = true;
        }

        return Result.Success();
    }

    public Result Deactivate(Instant timestamp)
    {
        if (IsActive)
        {
            IsActive = false;
        }

        return Result.Success();
    }

    public Result Update(WorkStatusCategory workStatusCategory, int order, Instant timestamp)
    {
        try
        {
            WorkStatusCategory = workStatusCategory;
            Order = order;

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.ToString());
        }
    }

    /// <summary>
    /// Used when creating a new workflow scheme and workflow together.  EF will set the WorkflowId when the workflow is saved.
    /// </summary>
    /// <param name="workflow"></param>
    /// <param name="workStatusId"></param>
    /// <param name="workStatusCategory"></param>
    /// <param name="order"></param>
    /// <param name="isActive"></param>
    /// <returns></returns>
    internal static WorkflowScheme Create(Workflow workflow, int workStatusId, WorkStatusCategory workStatusCategory, int order, bool isActive)
    {
        return new(workflow, workStatusId, workStatusCategory, order, isActive);
    }
}
