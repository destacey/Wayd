namespace Wayd.ProductManagement.Application.DeploymentEnvironments.Commands;

/// <summary>
/// Takes an environment out of use, or puts it back.
/// </summary>
/// <remarks>
/// Deployments already recorded against it stand — they are history, and the delivery measures read
/// them. This only stops new ones.
/// </remarks>
public sealed record SetDeploymentEnvironmentActiveCommand(Guid Id, bool IsActive) : ICommand;

public sealed class SetDeploymentEnvironmentActiveCommandValidator
    : AbstractValidator<SetDeploymentEnvironmentActiveCommand>
{
    public SetDeploymentEnvironmentActiveCommandValidator()
    {
        RuleFor(e => e.Id)
            .NotEmpty();
    }
}

public sealed class SetDeploymentEnvironmentActiveCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    ILogger<SetDeploymentEnvironmentActiveCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<SetDeploymentEnvironmentActiveCommand>
{
    private const string AppRequestName = nameof(SetDeploymentEnvironmentActiveCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<SetDeploymentEnvironmentActiveCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(
        SetDeploymentEnvironmentActiveCommand request, CancellationToken cancellationToken)
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

            var result = request.IsActive
                ? environment.Activate()
                : environment.Deactivate(EventActor.User(_currentUser.GetUserId()), _dateTimeProvider.Now);

            if (result.IsFailure)
            {
                _logger.LogInformation(
                    "Unable to change Deployment Environment {EnvironmentId} activation. Error message: {Error}",
                    request.Id, result.Error);
                return Result.Failure(result.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Deployment Environment {EnvironmentId} active set to {IsActive}.", request.Id, request.IsActive);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
