using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Workspaces.Commands;

public sealed class ChangeExternalWorkspaceWorkProcessCommandHandler(IWorkDbContext workDbContext, IDateTimeProvider dateTimeProvider, ILogger<ChangeExternalWorkspaceWorkProcessCommandHandler> logger) : ICommandHandler<ChangeExternalWorkspaceWorkProcessCommand>
{
    private const string AppRequestName = nameof(ChangeExternalWorkspaceWorkProcessCommand);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<ChangeExternalWorkspaceWorkProcessCommandHandler> _logger = logger;

    public async Task<Result> Handle(ChangeExternalWorkspaceWorkProcessCommand request, CancellationToken cancellationToken)
    {
        var workspace = await _workDbContext.Workspaces.FirstOrDefaultAsync(w => w.Id == request.WorkspaceId, cancellationToken);
        if (workspace is null)
        {
            _logger.LogWarning("{AppRequestName}: workspace {WorkspaceId} not found.", AppRequestName, request.WorkspaceId);
            return Result.Failure($"Workspace {request.WorkspaceId} not found.");
        }

        var newWorkProcess = await _workDbContext.WorkProcesses.FirstOrDefaultAsync(wp => wp.ExternalId == request.NewWorkProcessExternalId, cancellationToken);
        if (newWorkProcess is null)
        {
            _logger.LogWarning("{AppRequestName}: work process with external ID {WorkProcessExternalId} not found. The process may not be initialized yet.", AppRequestName, request.NewWorkProcessExternalId);
            return Result.Failure($"Work process with external ID {request.NewWorkProcessExternalId} not found. The process may not be initialized yet.");
        }

        if (workspace.WorkProcessId == newWorkProcess.Id)
            return Result.Success();

        var result = workspace.ChangeWorkProcess(newWorkProcess.Id, _dateTimeProvider.Now);
        if (result.IsFailure)
        {
            _logger.LogError("{AppRequestName}: failed to change work process for workspace {WorkspaceId}. Error: {Error}", AppRequestName, workspace.Id, result.Error);
            return result;
        }

        await _workDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("{AppRequestName}: changed workspace {WorkspaceName} work process to {WorkProcessName}.", AppRequestName, workspace.Name, newWorkProcess.Name);

        return Result.Success();
    }
}
