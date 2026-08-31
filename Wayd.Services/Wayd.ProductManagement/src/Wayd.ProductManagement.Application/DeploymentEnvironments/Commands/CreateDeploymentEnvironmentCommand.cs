using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.DeploymentEnvironments.Commands;

public sealed record CreateDeploymentEnvironmentCommand(
    string Name,
    EnvironmentCategory Category,
    int RingOrder) : ICommand<ObjectIdAndKey>;

public sealed class CreateDeploymentEnvironmentCommandValidator
    : AbstractValidator<CreateDeploymentEnvironmentCommand>
{
    public CreateDeploymentEnvironmentCommandValidator()
    {
        RuleFor(e => e.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(e => e.Category)
            .IsInEnum();

        RuleFor(e => e.RingOrder)
            .GreaterThanOrEqualTo(0);
    }
}

public sealed class CreateDeploymentEnvironmentCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    ILogger<CreateDeploymentEnvironmentCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CreateDeploymentEnvironmentCommand, ObjectIdAndKey>
{
    private const string AppRequestName = nameof(CreateDeploymentEnvironmentCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<CreateDeploymentEnvironmentCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result<ObjectIdAndKey>> Handle(
        CreateDeploymentEnvironmentCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var name = request.Name.Trim();

            if (await _productManagementDbContext.DeploymentEnvironments
                    .AnyAsync(e => e.Name == name, cancellationToken))
            {
                return Result.Failure<ObjectIdAndKey>($"An environment named '{name}' already exists.");
            }

            var environment = DeploymentEnvironment.Create(
                name,
                request.Category,
                request.RingOrder,
                EventActor.User(_currentUser.GetUserId()),
                _dateTimeProvider.Now);

            await _productManagementDbContext.DeploymentEnvironments.AddAsync(environment, cancellationToken);
            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deployment Environment {EnvironmentId} created.", environment.Id);

            return Result.Success(new ObjectIdAndKey(environment.Id, environment.Key));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<ObjectIdAndKey>($"Error handling {AppRequestName} command.");
        }
    }
}
