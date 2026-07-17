using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;

namespace Wayd.Work.Domain.Models;

public sealed class WorkProcessScheme : BaseSoftDeletableEntity, IActivatable
{
    private WorkProcessScheme() { }

    internal WorkProcessScheme(Guid workProcessId, int workTypeId, Guid? workflowId)
    {
        WorkProcessId = workProcessId;
        WorkTypeId = workTypeId;
        WorkflowId = workflowId;
    }
    public Guid WorkProcessId { get; }
    public WorkProcess? WorkProcess { get; private set; }
    public int WorkTypeId { get; }
    public WorkType? WorkType { get; private set; }
    public Guid? WorkflowId { get; private set; }
    public Workflow? Workflow { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// The process for activating a work process scheme.
    /// </summary>
    /// <param name="timestamp"></param>
    /// <returns>Result that indicates success or a list of errors</returns>
    public Result Activate(Instant timestamp)
    {
        if (!IsActive)
        {
            IsActive = true;
        }

        return Result.Success();
    }

    /// <summary>
    /// The process for deactivating a work process scheme.
    /// </summary>
    /// <param name="timestamp"></param>
    /// <returns>Result that indicates success or a list of errors</returns>
    public Result Deactivate(Instant timestamp)
    {
        Guard.Against.Null(WorkProcess, nameof(WorkProcess));

        if (IsActive)
        {
            IsActive = false;
        }

        return Result.Success();
    }

    internal Result ChangeWorkflow(Guid workflowId)
    {
        Guard.Against.Default(workflowId, nameof(workflowId));

        WorkflowId = workflowId;

        return Result.Success();
    }

    internal static WorkProcessScheme CreateExternal(Guid workProcessId, int workTypeId, Guid workflowId, bool isActive)
    {
        Guard.Against.Default(workProcessId, nameof(workProcessId));
        Guard.Against.Default(workflowId, nameof(workflowId));

        var scheme = new WorkProcessScheme(workProcessId, workTypeId, workflowId);

        // external work process schemes do not have to be active when they are first created in Wayd
        if (scheme.IsActive != isActive)
            scheme.IsActive = isActive;

        return scheme;
    }
}
