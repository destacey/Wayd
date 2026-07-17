using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Domain.Enums;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Workspaces.Commands;

public sealed class SetSystemIdOnExternalWorkspacesCommandHandler(IWorkDbContext workDbContext, ILogger<SetSystemIdOnExternalWorkspacesCommandHandler> logger) : ICommandHandler<SetSystemIdOnExternalWorkspacesCommand>
{
    private const string AppRequestName = nameof(SetSystemIdOnExternalWorkspacesCommand);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<SetSystemIdOnExternalWorkspacesCommandHandler> _logger = logger;

    public async Task<Result> Handle(SetSystemIdOnExternalWorkspacesCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var workspaces = await _workDbContext.Workspaces
                .Where(w => w.OwnershipInfo.Ownership == Ownership.Managed
                    && w.OwnershipInfo.Connector == request.Connector
                    && w.OwnershipInfo.SystemId == null
                    && request.WorkspaceIds.Contains(w.Id))
                .ToListAsync(cancellationToken);

            if (workspaces.Count == 0)
                return Result.Success();

            foreach (var workspace in workspaces)
            {
                var result = workspace.SetSystemId(request.SystemId);
                if (result.IsFailure)
                {
                    _logger.LogError("Failed to set SystemId for workspace {WorkspaceId}. Error: {Error}", workspace.Id, result.Error);
                    return Result.Failure(result.Error);
                }
            }

            await _workDbContext.SaveChangesAsync(cancellationToken);

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("Successfully set SystemId for {Count} workspaces.", workspaces.Count);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
