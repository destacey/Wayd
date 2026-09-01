using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.Deployments.Commands;

/// <summary>
/// Records that a deployment reached its environment and was then reverted.
/// </summary>
/// <remarks>
/// Permitted only from a succeeded deployment. A failed or in-flight one never finished reaching its
/// environment, so counting it as a rollback would inflate change failure rate.
/// </remarks>
public sealed record RollBackDeploymentCommand(Guid Id, string? Reason, Instant? RolledBackAt) : ICommand, IRequireLinkedEmployee;

public sealed class RollBackDeploymentCommandValidator : AbstractValidator<RollBackDeploymentCommand>
{
    public RollBackDeploymentCommandValidator()
    {
        RuleFor(d => d.Id)
            .NotEmpty();

        RuleFor(d => d.Reason)
            .MaximumLength(1024);
    }
}

public sealed class RollBackDeploymentCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    ILogger<RollBackDeploymentCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<RollBackDeploymentCommand>
{
    private const string AppRequestName = nameof(RollBackDeploymentCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<RollBackDeploymentCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(RollBackDeploymentCommand request, CancellationToken cancellationToken)
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

            // Metrics read the alias rather than the status name, so resolving by meaning is what keeps
            // a renamed outcome counting.
            var status = await _statusResolver.ForAlias(
                ProductWorkflowOwners.Deployment.Key,
                scopeId: null,
                (int)ProductStatusAlias.RolledBack,
                cancellationToken);

            if (status.IsFailure)
            {
                _logger.LogError("Unable to resolve the rolled-back deployment status. Error message: {Error}", status.Error);
                return Result.Failure(status.Error);
            }

            var environmentName = await _productManagementDbContext.DeploymentEnvironments
                .Where(e => e.Id == deployment.EnvironmentId)
                .Select(e => e.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

            // Read per scope rather than from the claim snapshot, which a personal access token
            // freezes for its whole lifetime. This value is frozen onto the transition, so a stale
            // one would misattribute the change permanently.
            var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);

            var result = deployment.RollBack(
                request.RolledBackAt ?? _dateTimeProvider.Now,
                request.Reason,
                status.Value,
                environmentName,
                EventActor.User(_currentUser.GetUserId(), employeeId),
                _dateTimeProvider.Now);

            if (result.IsFailure)
            {
                deployment.ClearDomainEvents();

                _logger.LogInformation(
                    "Unable to roll back Deployment {DeploymentId}. Error message: {Error}", request.Id, result.Error);
                return Result.Failure(result.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deployment {DeploymentId} rolled back.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
