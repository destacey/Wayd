using Wayd.Common.Domain.Enums.ProductManagement;

namespace Wayd.ProductManagement.Application.DeploymentEnvironments.Commands;

/// <summary>
/// Renames an environment, repositions it in the rollout order, and sets what kind of target it is.
/// </summary>
/// <remarks>
/// Reclassifying is the consequential half: every delivery measure scoped to production counts on the
/// category, so moving an environment in or out of Production changes what past deployments mean. The
/// aggregate raises an event for that reason and this does not hide it behind the rename.
/// </remarks>
public sealed record UpdateDeploymentEnvironmentCommand(
    Guid Id,
    string Name,
    EnvironmentCategory Category,
    int RingOrder) : ICommand;

public sealed class UpdateDeploymentEnvironmentCommandValidator
    : AbstractValidator<UpdateDeploymentEnvironmentCommand>
{
    public UpdateDeploymentEnvironmentCommandValidator()
    {
        RuleFor(e => e.Id)
            .NotEmpty();

        RuleFor(e => e.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(e => e.Category)
            .IsInEnum();

        RuleFor(e => e.RingOrder)
            .GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateDeploymentEnvironmentCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    ILogger<UpdateDeploymentEnvironmentCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UpdateDeploymentEnvironmentCommand>
{
    private const string AppRequestName = nameof(UpdateDeploymentEnvironmentCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<UpdateDeploymentEnvironmentCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(UpdateDeploymentEnvironmentCommand request, CancellationToken cancellationToken)
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

            var name = request.Name.Trim();

            if (await _productManagementDbContext.DeploymentEnvironments
                    .AnyAsync(e => e.Id != request.Id && e.Name == name, cancellationToken))
            {
                return Result.Failure($"An environment named '{name}' already exists.");
            }

            var updateResult = environment.Update(name, request.RingOrder);
            if (updateResult.IsFailure)
            {
                _logger.LogInformation(
                    "Unable to update Deployment Environment {EnvironmentId}. Error message: {Error}",
                    request.Id, updateResult.Error);
                return Result.Failure(updateResult.Error);
            }

            var reclassifyResult = environment.Reclassify(
                request.Category, EventActor.User(_currentUser.GetUserId()), _dateTimeProvider.Now);

            if (reclassifyResult.IsFailure)
            {
                environment.ClearDomainEvents();
                return Result.Failure(reclassifyResult.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deployment Environment {EnvironmentId} updated.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
