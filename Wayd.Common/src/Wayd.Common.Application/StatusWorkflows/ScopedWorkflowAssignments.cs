using CSharpFunctionalExtensions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.StatusWorkflows;

/// <summary>
/// Which workflow governs one scope, and pointing that scope at another, for a module whose own screens choose
/// a scope's workflow rather than workflow administration in Settings.
/// </summary>
/// <remarks>
/// A module cannot reach the engine's tables — its database view is its own — so it asks through this. Only
/// assigning a scope that holds no records is offered: moving a scope's records onto another workflow is a
/// migration, which needs a remap the operator decides, and which a scoped migrator has to support.
/// </remarks>
public interface IScopedWorkflowAssignments : IScopedService
{
    /// <summary>
    /// The workflow governing the scope, and whether the scope has an assignment of its own rather than
    /// following the organization's.
    /// </summary>
    /// <returns>A failure when nothing is assigned, not even organization-wide.</returns>
    Task<Result<ScopedWorkflow>> Current(string ownerType, Guid scopeId, CancellationToken cancellationToken);

    /// <summary>The published workflows of the owner type, which a scope may be pointed at, by name.</summary>
    Task<IReadOnlyList<WorkflowChoice>> Published(string ownerType, CancellationToken cancellationToken);

    /// <summary>
    /// Points a scope that holds no records at a published workflow of its owner type, giving it an assignment
    /// of its own. Saves.
    /// </summary>
    /// <remarks>
    /// The caller guarantees the scope holds no records. Choosing the organization's workflow removes the
    /// scope's own assignment, so the scope follows the organization's again, and a later change to it reaches
    /// the scope. A scope already following the workflow is left as it is.
    /// </remarks>
    Task<Result> AssignToEmptyScope(string ownerType, Guid scopeId, Guid workflowId, EventActor actor, Instant timestamp, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a scope's own assignment, when it has one, because the scope itself is being removed. Does not
    /// save: the caller saves it together with the scope's removal.
    /// </summary>
    Task<Result> RemoveScope(string ownerType, Guid scopeId, EventActor actor, Instant timestamp, CancellationToken cancellationToken);
}

/// <summary>The workflow governing a scope.</summary>
/// <param name="Workflow">The workflow.</param>
/// <param name="IsScopeOwn">Whether the scope has an assignment of its own; false when it follows the organization's.</param>
public sealed record ScopedWorkflow(WorkflowChoice Workflow, bool IsScopeOwn);

/// <summary>A workflow, named enough to choose between.</summary>
public sealed record WorkflowChoice(Guid Id, int Key, string Name, string? Description);

/// <inheritdoc cref="IScopedWorkflowAssignments"/>
public sealed class ScopedWorkflowAssignments(IStatusWorkflowDbContext dbContext) : IScopedWorkflowAssignments
{
    private readonly IStatusWorkflowDbContext _dbContext = dbContext;

    public async Task<Result<ScopedWorkflow>> Current(string ownerType, Guid scopeId, CancellationToken cancellationToken)
    {
        var descriptor = WorkflowOwners.Resolve(ownerType);
        if (descriptor.IsFailure)
            return Result.Failure<ScopedWorkflow>(descriptor.Error);

        var key = descriptor.Value.Key;

        // The scope's own assignment first, then the organization's, as the resolver reads them.
        var assigned = await _dbContext.WorkflowAssignments
            .Where(a => a.OwnerType == key && (a.ScopeId == scopeId || a.ScopeId == null))
            .OrderByDescending(a => a.ScopeId != null)
            .Join(_dbContext.StatusWorkflows, a => a.WorkflowId, w => w.Id,
                (a, w) => new { IsScopeOwn = a.ScopeId != null, w.Id, w.Key, w.Name, w.Description })
            .FirstOrDefaultAsync(cancellationToken);

        return assigned is null
            ? Result.Failure<ScopedWorkflow>($"No workflow is assigned for {descriptor.Value.DisplayName}. An administrator must assign one.")
            : Result.Success(new ScopedWorkflow(
                new WorkflowChoice(assigned.Id, assigned.Key, assigned.Name, assigned.Description), assigned.IsScopeOwn));
    }

    public async Task<IReadOnlyList<WorkflowChoice>> Published(string ownerType, CancellationToken cancellationToken)
    {
        var descriptor = WorkflowOwners.Resolve(ownerType);
        if (descriptor.IsFailure)
            return [];

        var key = descriptor.Value.Key;

        var workflows = await _dbContext.StatusWorkflows
            .Where(w => w.OwnerType == key && w.State == StatusWorkflowState.Published)
            .Select(w => new { w.Id, w.Key, w.Name, w.Description })
            .ToListAsync(cancellationToken);

        return
        [
            .. workflows
                .OrderBy(w => w.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(w => w.Key)
                .Select(w => new WorkflowChoice(w.Id, w.Key, w.Name, w.Description)),
        ];
    }

    public async Task<Result> AssignToEmptyScope(string ownerType, Guid scopeId, Guid workflowId, EventActor actor, Instant timestamp, CancellationToken cancellationToken)
    {
        var descriptor = WorkflowOwners.Resolve(ownerType);
        if (descriptor.IsFailure)
            return Result.Failure(descriptor.Error);

        var key = descriptor.Value.Key;

        var workflow = await _dbContext.StatusWorkflows
            .Include(w => w.Statuses)
            .Include(w => w.Transitions)
                .ThenInclude(t => t.Conditions)
            .AsSplitQuery()
            .FirstOrDefaultAsync(w => w.Id == workflowId && w.OwnerType == key, cancellationToken);
        if (workflow is null)
            return Result.Failure("Status workflow not found.");

        var current = await Current(key, scopeId, cancellationToken);
        if (current.IsSuccess && current.Value.Workflow.Id == workflow.Id)
            return Result.Success();

        var own = await _dbContext.WorkflowAssignments
            .FirstOrDefaultAsync(a => a.OwnerType == key && a.ScopeId == scopeId, cancellationToken);

        var organizationWorkflowId = await OrganizationWorkflowId(key, cancellationToken);
        if (own is not null && workflow.Id == organizationWorkflowId)
        {
            var removed = own.Remove(organizationWorkflowId, actor, timestamp);
            if (removed.IsFailure)
                return removed;

            _dbContext.WorkflowAssignments.Remove(own);
        }
        else if (own is null)
        {
            var created = WorkflowAssignment.Create(key, scopeId, workflow, actor, timestamp);
            if (created.IsFailure)
                return Result.Failure(created.Error);

            _dbContext.WorkflowAssignments.Add(created.Value);
        }
        else
        {
            var from = await _dbContext.StatusWorkflows
                .Include(w => w.Statuses)
                .FirstAsync(w => w.Id == own.WorkflowId, cancellationToken);

            // The scope holds no records, so there is nothing for the mapping to decide: every status it cannot
            // match goes to the target's initial one, only so the assignment's completeness check is met.
            var remap = StatusRemap.AutoMap(from, workflow);
            if (remap.IsFailure)
                return Result.Failure(remap.Error);
            if (workflow.InitialStatus is { } initial)
            {
                foreach (var unresolved in remap.Value.Unresolved.ToList())
                    remap.Value.Resolve(unresolved.Id, initial);
            }

            var reassigned = own.ReassignTo(workflow, remap.Value, actor, timestamp);
            if (reassigned.IsFailure)
            {
                own.ClearDomainEvents();
                return reassigned;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> RemoveScope(string ownerType, Guid scopeId, EventActor actor, Instant timestamp, CancellationToken cancellationToken)
    {
        var descriptor = WorkflowOwners.Resolve(ownerType);
        if (descriptor.IsFailure)
            return Result.Failure(descriptor.Error);

        var key = descriptor.Value.Key;

        var own = await _dbContext.WorkflowAssignments
            .FirstOrDefaultAsync(a => a.OwnerType == key && a.ScopeId == scopeId, cancellationToken);
        if (own is null)
            return Result.Success();

        var removed = own.Remove(organizationWorkflowId: null, actor, timestamp);
        if (removed.IsFailure)
            return removed;

        _dbContext.WorkflowAssignments.Remove(own);

        return Result.Success();
    }

    private Task<Guid?> OrganizationWorkflowId(string ownerType, CancellationToken cancellationToken) =>
        _dbContext.WorkflowAssignments
            .Where(a => a.OwnerType == ownerType && a.ScopeId == null)
            .Select(a => (Guid?)a.WorkflowId)
            .FirstOrDefaultAsync(cancellationToken);
}
