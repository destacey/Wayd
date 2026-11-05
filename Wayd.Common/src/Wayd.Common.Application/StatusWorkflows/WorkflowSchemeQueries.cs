using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.Common.Application.StatusWorkflows;

/// <summary>
/// Lookups over workflow schemes that more than one handler needs.
/// </summary>
public static class WorkflowSchemeQueries
{
    /// <summary>
    /// The workflow's implicit default-only scheme, added to the context unsaved when it has none yet.
    /// </summary>
    /// <remarks>
    /// How a scope that picks a workflow — every scope of an owner type without record types — comes to
    /// point at a scheme. One per workflow, shared by every scope that picks it.
    /// </remarks>
    public static async Task<Result<StatusWorkflowScheme>> ImplicitSchemeFor(
        this IStatusWorkflowDbContext dbContext, StatusWorkflow workflow, EventActor actor, Instant timestamp, CancellationToken cancellationToken)
    {
        var existing = await dbContext.StatusWorkflowSchemes
            .Include(s => s.Entries)
            .FirstOrDefaultAsync(s => s.IsImplicit && s.DefaultWorkflowId == workflow.Id, cancellationToken);

        if (existing is not null)
        {
            return Result.Success(existing);
        }

        var created = StatusWorkflowScheme.CreateImplicit(workflow, actor, timestamp);
        if (created.IsSuccess)
        {
            dbContext.StatusWorkflowSchemes.Add(created.Value);
        }

        return created;
    }

    /// <summary>
    /// Every workflow some scope's scheme sends records to, as its default or through an entry. May repeat.
    /// </summary>
    public static IQueryable<Guid> AssignedWorkflowIds(this IStatusWorkflowDbContext dbContext) =>
        dbContext.WorkflowAssignments
            .Select(a => a.Scheme!.DefaultWorkflowId)
            .Concat(dbContext.WorkflowAssignments.SelectMany(a => a.Scheme!.Entries.Select(e => e.WorkflowId)));
}
