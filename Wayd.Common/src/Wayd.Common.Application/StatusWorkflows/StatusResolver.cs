using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.Common.Application.StatusWorkflows;

/// <inheritdoc cref="IStatusResolver"/>
public sealed class StatusResolver(IStatusWorkflowDbContext dbContext) : IStatusResolver
{
    private readonly IStatusWorkflowDbContext _dbContext = dbContext;

    public async Task<Result<StatusRef>> ForAlias(string ownerType, Guid? scopeId, int alias, CancellationToken cancellationToken)
    {
        var workflow = await ForScope(ownerType, scopeId, cancellationToken);
        if (workflow.IsFailure)
        {
            return Result.Failure<StatusRef>(workflow.Error);
        }

        var status = workflow.Value.StatusFor(alias);

        return status is null
            ? Result.Failure<StatusRef>($"'{workflow.Value.Name}' has no status for {DescribeAlias(ownerType, alias)}.")
            : Result.Success(workflow.Value.TargetRef(status.Id)!);
    }

    public async Task<Result<StatusRef>> Initial(string ownerType, Guid? scopeId, CancellationToken cancellationToken)
    {
        var workflow = await ForScope(ownerType, scopeId, cancellationToken);
        if (workflow.IsFailure)
        {
            return Result.Failure<StatusRef>(workflow.Error);
        }

        var status = workflow.Value.InitialStatus;

        return status is null
            ? Result.Failure<StatusRef>($"'{workflow.Value.Name}' has no statuses.")
            : Result.Success(workflow.Value.TargetRef(status.Id)!);
    }

    public async Task<Result<StatusWorkflow>> ForScope(string ownerType, Guid? scopeId, CancellationToken cancellationToken) =>
        (await ForRecordType(ownerType, WorkflowScopeChain.ForScope(scopeId), recordTypeId: null, cancellationToken))
            .Map(r => r.Workflow);

    public async Task<Result<ResolvedWorkflow>> ForRecordType(string ownerType, WorkflowScopeChain scopes, Guid? recordTypeId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        var descriptor = WorkflowOwners.Resolve(ownerType);
        if (descriptor.IsFailure)
        {
            return Result.Failure<ResolvedWorkflow>(descriptor.Error);
        }

        var key = descriptor.Value.Key;
        var scopeIds = scopes.ScopeIds.ToArray();

        // Every level's assignment at once — a chain is a handful of rows — then the most specific wins. A level
        // with no assignment of its own is the normal case, not an error.
        var candidates = await _dbContext.WorkflowAssignments
            .Where(a => a.OwnerType == key && (a.ScopeId == null || scopeIds.Contains(a.ScopeId.Value)))
            .Select(a => new
            {
                a.ScopeId,
                EntryWorkflowId = a.Scheme!.Entries
                    .Where(e => recordTypeId != null && e.RecordTypeId == recordTypeId)
                    .Select(e => (Guid?)e.WorkflowId)
                    .FirstOrDefault(),
                a.Scheme.DefaultWorkflowId,
            })
            .ToListAsync(cancellationToken);

        var decided = candidates.MinBy(c => scopes.Rank(c.ScopeId));
        if (decided is null)
        {
            return Result.Failure<ResolvedWorkflow>(
                $"No workflow is assigned for {descriptor.Value.DisplayName}. An administrator must assign one.");
        }

        var workflowId = decided.EntryWorkflowId ?? decided.DefaultWorkflowId;
        var source = new WorkflowSource(decided.ScopeId, IsRecordTypeEntry: decided.EntryWorkflowId is not null);

        // Transitions and their conditions are loaded with the statuses: every reference built from this
        // workflow carries the moves it allows and what each needs, and a missing include would silently
        // allow every move, or drop every condition.
        var workflow = await _dbContext.StatusWorkflows
            .Include(w => w.Statuses)
            .Include(w => w.Transitions)
                .ThenInclude(t => t.Conditions)
            .AsSplitQuery()
            .FirstOrDefaultAsync(w => w.Id == workflowId, cancellationToken);

        return workflow is null
            ? Result.Failure<ResolvedWorkflow>($"The workflow assigned for {descriptor.Value.DisplayName} no longer exists.")
            : Result.Success(new ResolvedWorkflow(workflow, source));
    }

    private static string DescribeAlias(string ownerType, int alias) =>
        WorkflowOwners.Resolve(ownerType) is { IsSuccess: true } descriptor
            ? descriptor.Value.DescribeAlias(alias)
            : alias.ToString();
}
