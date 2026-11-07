using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Commands;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkProcesses;
using Wayd.Work.Domain;

namespace Wayd.Work.Application.Workspaces.Commands;

/// <summary>
/// Assigns a workspace the managed scheme of its current work process.
/// </summary>
/// <remarks>
/// A process with no scheme — its workflows have not reached the engine yet, or none of its work types has
/// an active state — leaves the workspace with no assignment, so it never keeps the scheme of a process it
/// has left. The process's own sync (<see cref="SyncExternalWorkProcessWorkflowsCommand"/>) assigns it once
/// the scheme exists.
/// </remarks>
public sealed class SyncExternalWorkspaceWorkflowAssignmentCommandHandler(
    IWorkDbContext workDbContext,
    IStatusWorkflowDbContext statusWorkflowDbContext,
    IScopedWorkflowAssignments workflowAssignments,
    IDateTimeProvider dateTimeProvider,
    IDispatcher dispatcher,
    ILogger<SyncExternalWorkspaceWorkflowAssignmentCommandHandler> logger)
    : ICommandHandler<SyncExternalWorkspaceWorkflowAssignmentCommand>
{
    private const string AppRequestName = nameof(SyncExternalWorkspaceWorkflowAssignmentCommand);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IStatusWorkflowDbContext _statusWorkflowDbContext = statusWorkflowDbContext;
    private readonly IScopedWorkflowAssignments _workflowAssignments = workflowAssignments;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ILogger<SyncExternalWorkspaceWorkflowAssignmentCommandHandler> _logger = logger;

    public async Task<Result> Handle(SyncExternalWorkspaceWorkflowAssignmentCommand request, CancellationToken cancellationToken)
    {
        var workspace = await (
            from w in _workDbContext.Workspaces
            join p in _workDbContext.WorkProcesses on w.WorkProcessId equals p.Id
            where w.Id == request.WorkspaceId
            select new { w.Id, w.Name, ProcessExternalId = p.ExternalId })
            .FirstOrDefaultAsync(cancellationToken);
        if (workspace is null)
        {
            _logger.LogWarning("{AppRequestName}: workspace {WorkspaceId} or its work process not found.", AppRequestName, request.WorkspaceId);
            return Result.Failure($"Workspace {request.WorkspaceId} or its work process not found.");
        }

        var ownerType = WorkWorkflowOwners.WorkItem.Key;
        string? schemeKey = workspace.ProcessExternalId is { } processExternalId ? ManagedWorkflowKeys.Scheme(processExternalId) : null;
        var schemeExists = schemeKey is not null && await _statusWorkflowDbContext.StatusWorkflowSchemes
            .AnyAsync(s => s.OwnerType == ownerType && s.ExternalKey == schemeKey, cancellationToken);
        if (!schemeExists)
        {
            _logger.LogWarning("{AppRequestName}: the work process of workspace {WorkspaceName} has no managed scheme, so the workspace is left unassigned.", AppRequestName, workspace.Name);
            return await Unassign(workspace.Id, ownerType, cancellationToken);
        }

        var result = await _dispatcher.Send(new SyncManagedWorkflowAssignmentCommand(ownerType, workspace.Id, schemeKey!), cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogError("{AppRequestName}: unable to assign workspace {WorkspaceName} its scheme. Error: {Error}", AppRequestName, workspace.Name, result.Error);
            return Result.Failure(result.Error);
        }

        return Result.Success();
    }

    private async Task<Result> Unassign(Guid workspaceId, string ownerType, CancellationToken cancellationToken)
    {
        var removed = await _workflowAssignments.RemoveScope(ownerType, workspaceId, EventActor.Sync(null), _dateTimeProvider.Now, cancellationToken);
        if (removed.IsFailure)
        {
            _logger.LogError("{AppRequestName}: unable to unassign workspace {WorkspaceId}. Error: {Error}", AppRequestName, workspaceId, removed.Error);
            return removed;
        }

        await _statusWorkflowDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
