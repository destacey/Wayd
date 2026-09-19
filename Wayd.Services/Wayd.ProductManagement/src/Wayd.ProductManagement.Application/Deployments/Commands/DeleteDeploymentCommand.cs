namespace Wayd.ProductManagement.Application.Deployments.Commands;

/// <summary>
/// Permanently deletes a deployment and its status history.
/// </summary>
/// <remarks>
/// The delivery measures and the rollout stop counting it. For a deployment recorded by mistake, or for
/// purging a retired product's history.
/// </remarks>
public sealed record DeleteDeploymentCommand(Guid Id) : ICommand;

public sealed class DeleteDeploymentCommandValidator : AbstractValidator<DeleteDeploymentCommand>
{
    public DeleteDeploymentCommandValidator()
    {
        RuleFor(d => d.Id)
            .NotEmpty();
    }
}

public sealed class DeleteDeploymentCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusWorkflowDbContext statusWorkflowDbContext,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    ILogger<DeleteDeploymentCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<DeleteDeploymentCommand>
{
    private const string AppRequestName = nameof(DeleteDeploymentCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusWorkflowDbContext _statusWorkflowDbContext = statusWorkflowDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<DeleteDeploymentCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(DeleteDeploymentCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var deployment = await _productManagementDbContext.Deployments
                .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);

            if (deployment is null)
            {
                _logger.LogInformation("Deployment {DeploymentId} not found.", request.Id);
                return Result.Failure("Deployment not found.");
            }

            // The history table serves every status-tracked type and has no foreign key to any of them,
            // so nothing cascades: left behind, these rows would still surface in the delivery overview.
            // Both interfaces are views over one context, so the single save below removes both.
            var transitions = await _statusWorkflowDbContext.StatusTransitions
                .Where(t => t.OwnerType == deployment.StatusOwnerType && t.RecordId == deployment.Id)
                .ToListAsync(cancellationToken);

            var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);

            deployment.Delete(EventActor.User(_currentUser.GetUserId(), employeeId), _dateTimeProvider.Now);

            _statusWorkflowDbContext.StatusTransitions.RemoveRange(transitions);
            _productManagementDbContext.Deployments.Remove(deployment);
            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deployment {DeploymentId} deleted.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
