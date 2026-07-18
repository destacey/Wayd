using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Workspaces.Commands;

public sealed record SetExternalViewWorkItemUrlTemplateCommand(Guid WorkspaceId, string? ExternalViewWorkItemUrlTemplate) : ICommand;

public sealed class SetExternalViewWorkItemUrlTemplateCommandHandler(IWorkDbContext workDbContext, ILogger<SetExternalViewWorkItemUrlTemplateCommandHandler> logger, IDateTimeProvider dateTimeProvider) : ICommandHandler<SetExternalViewWorkItemUrlTemplateCommand>
{
    private const string AppRequestName = nameof(SetExternalViewWorkItemUrlTemplateCommand);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<SetExternalViewWorkItemUrlTemplateCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(SetExternalViewWorkItemUrlTemplateCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var workspace = await _workDbContext.Workspaces.FirstOrDefaultAsync(w => w.Id == request.WorkspaceId, cancellationToken);
            if (workspace is null)
            {
                _logger.LogWarning("Unable to set external view work item URL template for workspace {WorkspaceExternalId}. Workspace not found.", request.WorkspaceId);
                return Result.Failure($"Unable to set external view work item URL template for workspace {{request.ExternalWorkspaceId}}. Workspace not found.");
            }

            var result = workspace.SetExternalViewWorkItemUrlTemplate(request.ExternalViewWorkItemUrlTemplate, _dateTimeProvider.Now);
            if (result.IsFailure)
            {
                _logger.LogError("Failed to set external view work item URL template for workspace {WorkspaceExternalId}. Error: {Error}", request.WorkspaceId, result.Error);
                return Result.Failure(result.Error);
            }
            await _workDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Successfully set external view work item URL template for workspace {WorkspaceExternalId}.", request.WorkspaceId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
