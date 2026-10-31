using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Work.Application.Workflows.Dtos;
using Wayd.Work.Domain.Interfaces;

namespace Wayd.Work.Application.Workflows;

/// <summary>
/// Builds the workflow for an external work type from its states.
/// </summary>
internal static class ExternalWorkflowBuilder
{
    /// <summary>
    /// A new workflow for <paramref name="workTypeWorkflow"/>; fails when a state names a work
    /// status not in <paramref name="workStatuses"/>.
    /// </summary>
    public static Result<Workflow> Build(string name, string? description, IExternalWorkTypeWorkflow workTypeWorkflow, IReadOnlyCollection<WorkStatus> workStatuses)
    {
        List<ICreateWorkflowScheme> schemes = new(workTypeWorkflow.WorkflowStates.Count);
        foreach (var state in workTypeWorkflow.WorkflowStates)
        {
            var workStatus = workStatuses.FirstOrDefault(s => s.Name == state.StatusName);
            if (workStatus is null)
                return Result.Failure<Workflow>($"Work status {state.StatusName} not found.");

            schemes.Add(CreateWorkflowSchemeDto.Create(workStatus.Id, state.Category, state.Order, state.IsActive));
        }

        return Workflow.CreateExternal(name, description, schemes);
    }
}
