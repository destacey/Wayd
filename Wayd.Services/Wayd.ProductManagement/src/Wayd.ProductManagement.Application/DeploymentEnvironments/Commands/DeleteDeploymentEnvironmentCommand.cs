using Wayd.Common.Domain.Authorization;
using Wayd.ProductManagement.Application.Deployments;

namespace Wayd.ProductManagement.Application.DeploymentEnvironments.Commands;

/// <summary>
/// Permanently deletes an environment, every deployment into it, and their status history.
/// </summary>
/// <remarks>
/// The delivery measures and the rollout stop counting those deployments. Retiring keeps them, and is
/// the everyday way out; this is for an environment defined by mistake, or for purging history.
/// </remarks>
public sealed record DeleteDeploymentEnvironmentCommand(Guid Id) : ICommand;

public sealed class DeleteDeploymentEnvironmentCommandValidator : AbstractValidator<DeleteDeploymentEnvironmentCommand>
{
    public DeleteDeploymentEnvironmentCommandValidator()
    {
        RuleFor(e => e.Id)
            .NotEmpty();
    }
}

public sealed class DeleteDeploymentEnvironmentCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusWorkflowDbContext statusWorkflowDbContext,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    ILogger<DeleteDeploymentEnvironmentCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<DeleteDeploymentEnvironmentCommand>
{
    private const string AppRequestName = nameof(DeleteDeploymentEnvironmentCommand);

    private static readonly string DeleteDeliveryPermission =
        ApplicationPermission.NameFor(ApplicationAction.Delete, ApplicationResource.Delivery);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusWorkflowDbContext _statusWorkflowDbContext = statusWorkflowDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<DeleteDeploymentEnvironmentCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(DeleteDeploymentEnvironmentCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var environment = await _productManagementDbContext.DeploymentEnvironments
                .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

            if (environment is null)
            {
                _logger.LogInformation("Deployment Environment {EnvironmentId} not found.", request.Id);
                return Result.Failure("Deployment environment not found.");
            }

            var deployments = await _productManagementDbContext.Deployments
                .Where(d => d.EnvironmentId == request.Id)
                .ToListAsync(cancellationToken);

            // The endpoint grants deleting the environment; taking deployments with it is deleting
            // deployments, which is its own grant. Only knowable once they are counted, so checked here.
            if (deployments.Count > 0
                && !await _currentPrincipal.HasPermission(DeleteDeliveryPermission, cancellationToken))
            {
                return Result.Failure(
                    $"Deployments into this environment would be deleted with it ({deployments.Count}), which also needs permission to delete deployments.");
            }

            var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
            var actor = EventActor.User(_currentUser.GetUserId(), employeeId);
            var timestamp = _dateTimeProvider.Now;

            // Deployments.EnvironmentId restricts, so they go in the same save rather than being left to
            // fail the delete at the database.
            await DeploymentRemoval.Stage(
                _productManagementDbContext,
                _statusWorkflowDbContext,
                deployments,
                actor,
                timestamp,
                cancellationToken);

            environment.Delete(actor, timestamp);
            _productManagementDbContext.DeploymentEnvironments.Remove(environment);

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Deployment Environment {EnvironmentId} deleted with {DeploymentCount} deployments.",
                request.Id,
                deployments.Count);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
