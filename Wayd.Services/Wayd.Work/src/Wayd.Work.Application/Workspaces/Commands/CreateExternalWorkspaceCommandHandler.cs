using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Application.Validators;
using Wayd.Common.Domain.Models;
using Wayd.Common.Models;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Workspaces.Commands;

public sealed class CreateExternalWorkspaceCommandHandlerValidator : CustomValidator<CreateExternalWorkspaceCommand>
{
    private readonly IWorkDbContext _workDbContext;

    public CreateExternalWorkspaceCommandHandlerValidator(IWorkDbContext workDbContext)
    {
        _workDbContext = workDbContext;

        RuleFor(c => c.Connector)
            .IsInEnum();

        RuleFor(c => c.SystemId)
            .NotEmpty();

        RuleFor(c => c.ExternalWorkspace)
            .NotNull()
            .SetValidator(new IExternalWorkspaceConfigurationValidator());

        RuleFor(c => c.WorkspaceKey)
            .NotNull()
            .MustAsync(BeUniqueWorkspaceKey).WithMessage("The workspace key already exists.");

        RuleFor(c => c.WorkspaceKey.Value)
            .NotNull()
            .MaximumLength(20);

        RuleFor(c => c.WorkspaceName)
            .NotEmpty()
            .MaximumLength(128)
            .MustAsync(BeUniqueWorkspaceName).WithMessage("The workspace name already exists.");

        RuleFor(c => c.ExternalViewWorkItemUrlTemplate)
            .MaximumLength(256);
    }

    public async Task<bool> BeUniqueWorkspaceKey(WorkspaceKey workspaceKey, CancellationToken cancellationToken)
    {
        return await _workDbContext.Workspaces.AllAsync(x => x.Key != workspaceKey, cancellationToken);
    }

    public async Task<bool> BeUniqueWorkspaceName(string name, CancellationToken cancellationToken)
    {
        return await _workDbContext.Workspaces.AllAsync(x => x.Name != name, cancellationToken);
    }
}

public sealed class CreateExternalWorkspaceCommandHandler(IWorkDbContext workDbContext, IDateTimeProvider dateTimeProvider, ILogger<CreateExternalWorkspaceCommandHandler> logger) : ICommandHandler<CreateExternalWorkspaceCommand, IntegrationState<Guid>>
{
    private const string AppRequestName = nameof(CreateExternalWorkProcessCommand);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<CreateExternalWorkspaceCommandHandler> _logger = logger;

    public async Task<Result<IntegrationState<Guid>>> Handle(CreateExternalWorkspaceCommand request, CancellationToken cancellationToken)
    {
        var workProcess = await _workDbContext.WorkProcesses.FirstOrDefaultAsync(wp => wp.ExternalId == request.ExternalWorkspace.WorkProcessId, cancellationToken);
        if (workProcess is null || !workProcess.IsActive)
        {
            _logger.LogWarning("Unable to create an external workspace {WorkspaceExternalId} without an active work process {WorkProcessExternalId}.", request.ExternalWorkspace.Id, request.ExternalWorkspace.WorkProcessId);
            return Result.Failure<IntegrationState<Guid>>($"Unable to create an external workspace {{request.ExternalWorkspace.Id}} without an active work process {{request.ExternalWorkspace.WorkProcessId}}.");
        }

        var ownershipInfo = OwnershipInfo.CreateExternalOwned(request.Connector, request.SystemId, request.ExternalWorkspace.Id.ToString());

        var timestamp = _dateTimeProvider.Now;

        var workspace = Workspace.CreateExternal(request.WorkspaceKey, request.WorkspaceName, request.ExternalWorkspace.Description, ownershipInfo, workProcess.Id, request.ExternalViewWorkItemUrlTemplate, timestamp);

        _workDbContext.Workspaces.Add(workspace);
        await _workDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("{AppRequestName}: created Workspace {WorkspaceName}.", AppRequestName, workspace.Name);

        return Result.Success(IntegrationState<Guid>.Create(workspace.Id, workspace.IsActive));
    }
}
