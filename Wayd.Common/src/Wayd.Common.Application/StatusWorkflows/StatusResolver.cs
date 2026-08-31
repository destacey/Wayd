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
            : Result.Success(StatusRef.From(status));
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
            : Result.Success(StatusRef.From(status));
    }

    public async Task<Result<StatusWorkflow>> ForScope(string ownerType, Guid? scopeId, CancellationToken cancellationToken)
    {
        var descriptor = WorkflowOwners.Resolve(ownerType);
        if (descriptor.IsFailure)
        {
            return Result.Failure<StatusWorkflow>(descriptor.Error);
        }

        var key = descriptor.Value.Key;

        // The scope's own assignment, falling back to the organization-level one. Ordering puts the
        // narrower row first; a scope with no assignment of its own is the normal case, not an error.
        var workflowId = await _dbContext.WorkflowAssignments
            .Where(a => a.OwnerType == key && (a.ScopeId == scopeId || a.ScopeId == null))
            .OrderByDescending(a => a.ScopeId != null)
            .Select(a => (Guid?)a.WorkflowId)
            .FirstOrDefaultAsync(cancellationToken);

        if (workflowId is null)
        {
            return Result.Failure<StatusWorkflow>(
                $"No workflow is assigned for {descriptor.Value.DisplayName}. An administrator must assign one.");
        }

        var workflow = await _dbContext.StatusWorkflows
            .Include(w => w.Statuses)
            .FirstOrDefaultAsync(w => w.Id == workflowId, cancellationToken);

        return workflow is null
            ? Result.Failure<StatusWorkflow>($"The workflow assigned for {descriptor.Value.DisplayName} no longer exists.")
            : Result.Success(workflow);
    }

    private static string DescribeAlias(string ownerType, int alias) =>
        WorkflowOwners.Resolve(ownerType) is { IsSuccess: true } descriptor
            ? descriptor.Value.DescribeAlias(alias)
            : alias.ToString();
}
