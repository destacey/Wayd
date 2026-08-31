using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.Deployments.Commands;

/// <summary>
/// Records that a deployment did not reach its environment.
/// </summary>
/// <remarks>
/// Only counts toward change failure rate in production: a failure caught earlier is a failure that was
/// prevented, and counting it would invert the measure.
/// </remarks>
public sealed record FailDeploymentCommand(Guid Id, string? Reason, Instant? CompletedAt) : ICommand;

public sealed class FailDeploymentCommandValidator : AbstractValidator<FailDeploymentCommand>
{
    public FailDeploymentCommandValidator()
    {
        RuleFor(d => d.Id)
            .NotEmpty();

        RuleFor(d => d.Reason)
            .MaximumLength(1024);
    }
}

public sealed class FailDeploymentCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver,
    ICurrentUser currentUser,
    ILogger<FailDeploymentCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<FailDeploymentCommand>
{
    private const string AppRequestName = nameof(FailDeploymentCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<FailDeploymentCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(FailDeploymentCommand request, CancellationToken cancellationToken)
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
                (int)ProductStatusAlias.Failed,
                cancellationToken);

            if (status.IsFailure)
            {
                _logger.LogError("Unable to resolve the failed deployment status. Error message: {Error}", status.Error);
                return Result.Failure(status.Error);
            }

            var environmentName = await _productManagementDbContext.DeploymentEnvironments
                .Where(e => e.Id == deployment.EnvironmentId)
                .Select(e => e.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

            var result = deployment.Fail(
                request.CompletedAt ?? _dateTimeProvider.Now,
                request.Reason,
                status.Value,
                environmentName,
                EventActor.User(_currentUser.GetUserId()),
                _dateTimeProvider.Now);

            if (result.IsFailure)
            {
                deployment.ClearDomainEvents();

                _logger.LogInformation(
                    "Unable to fail Deployment {DeploymentId}. Error message: {Error}", request.Id, result.Error);
                return Result.Failure(result.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deployment {DeploymentId} failed.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
