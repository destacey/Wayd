using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.Tests.Shared.Data;

/// <summary>
/// Builds assignments the way a scope picking a workflow does: through the workflow's implicit
/// default-only scheme.
/// </summary>
public static class TestWorkflowAssignments
{
    /// <summary>
    /// Assigns <paramref name="workflow"/>'s implicit scheme to the scope. The scheme is reachable through
    /// <see cref="WorkflowAssignment.Scheme"/>; a fake context registering the assignment should register it too.
    /// </summary>
    public static Result<WorkflowAssignment> ForWorkflow(
        string ownerType, Guid? scopeId, StatusWorkflow workflow, EventActor actor, Instant timestamp)
    {
        var scheme = StatusWorkflowScheme.CreateImplicit(workflow, actor, timestamp);

        return scheme.IsFailure
            ? Result.Failure<WorkflowAssignment>(scheme.Error)
            : WorkflowAssignment.Create(ownerType, scopeId, scheme.Value, workflow, actor, timestamp);
    }
}
