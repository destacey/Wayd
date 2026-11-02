using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Infrastructure.Persistence.Context;

namespace Wayd.Infrastructure.IntegrationTests.Infrastructure;

/// <summary>
/// Builds assignments the way a scope picking a workflow does: through the workflow's implicit scheme,
/// reusing the one the database already holds so the one-per-workflow index is kept.
/// </summary>
public static class TestWorkflowAssignments
{
    /// <summary>
    /// Assigns <paramref name="workflow"/>'s implicit scheme to the scope. A scheme it creates is added to
    /// <paramref name="context"/>; the caller adds the assignment and saves.
    /// </summary>
    public static async Task<Result<WorkflowAssignment>> ForWorkflow(
        WaydDbContext context, string ownerType, Guid? scopeId, StatusWorkflow workflow, EventActor actor, Instant timestamp)
    {
        var scheme = await context.ImplicitSchemeFor(workflow, actor, timestamp, TestContext.Current.CancellationToken);

        return scheme.IsFailure
            ? Result.Failure<WorkflowAssignment>(scheme.Error)
            : WorkflowAssignment.Create(ownerType, scopeId, scheme.Value, workflow, actor, timestamp);
    }
}
