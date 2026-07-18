using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Application.Validators;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Workspaces.Commands;

public sealed class UpdateExternalWorkspaceCommandHandlerValidator : CustomValidator<UpdateExternalWorkspaceCommand>
{
    public UpdateExternalWorkspaceCommandHandlerValidator(IWorkDbContext workDbContext)
    {
        RuleFor(c => c.ExternalWorkspace)
            .NotNull()
            .SetValidator(new IExternalWorkspaceConfigurationValidator());
    }
}

public sealed class UpdateExternalWorkspaceCommandHandler(IWorkDbContext workDbContext, IDateTimeProvider dateTimeProvider, ILogger<UpdateExternalWorkspaceCommandHandler> logger) : ICommandHandler<UpdateExternalWorkspaceCommand>
{
    private const string AppRequestName = nameof(UpdateExternalWorkspaceCommand);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<UpdateExternalWorkspaceCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateExternalWorkspaceCommand request, CancellationToken cancellationToken)
    {
        var workspace = await _workDbContext.Workspaces.FirstOrDefaultAsync(wp => wp.OwnershipInfo.ExternalId == request.ExternalWorkspace.Id.ToString(), cancellationToken);
        if (workspace is null)
        {
            _logger.LogWarning("{AppRequestName}: workspace {WorkspaceExternalId} not found.", AppRequestName, request.ExternalWorkspace.Id);
            return Result.Failure($"Workspace {request.ExternalWorkspace.Id} not found.");
        }

        // TODO: The workspace name may have been changed, so don't update at this time.  This will be handled in a future release.
        var workspaceResult = workspace.Update(workspace.Name, request.ExternalWorkspace.Description, _dateTimeProvider.Now);
        if (workspaceResult.IsFailure)
        {
            // Reset the entity
            await _workDbContext.Entry(workspace).ReloadAsync(cancellationToken);
            workspace.ClearDomainEvents();

            _logger.LogError("{AppRequestName}: failed to update workspace {WorkspaceId}. Error: {Error}", AppRequestName, workspace.Id, workspaceResult.Error);
            return Result.Failure(workspaceResult.Error);
        }

        _workDbContext.Workspaces.Update(workspace);
        await _workDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("{AppRequestName}: updated Workspace {WorkspaceName}.", AppRequestName, workspace.Name);

        return Result.Success();
    }
}
