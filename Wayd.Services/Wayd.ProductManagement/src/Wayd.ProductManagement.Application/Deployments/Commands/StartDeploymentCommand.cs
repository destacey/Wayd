using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Deployments.Commands;

/// <summary>
/// Records that a release or package started reaching an environment.
/// </summary>
/// <param name="ReleaseId">
/// The release deployed. Exactly one of this and <paramref name="PackageId"/> is supplied: where a
/// package exists it is the unit, so one pipeline run counts once.
/// </param>
/// <param name="ArtifactId">
/// The build that actually shipped — <c>4.8.2.008</c> where the release version is <c>4.8.2</c>. Two
/// builds of one release are two deployments.
/// </param>
public sealed record StartDeploymentCommand(
    Guid? ReleaseId,
    Guid? PackageId,
    Guid EnvironmentId,
    string? ArtifactId,
    Instant? StartedAt) : ICommand<ObjectIdAndKey>;

public sealed class StartDeploymentCommandValidator : AbstractValidator<StartDeploymentCommand>
{
    public StartDeploymentCommandValidator()
    {
        RuleFor(d => d.EnvironmentId)
            .NotEmpty();

        RuleFor(d => d.ArtifactId)
            .MaximumLength(128);

        RuleFor(d => d)
            .Must(d => d.ReleaseId is not null ^ d.PackageId is not null)
            .WithMessage("A deployment is for either a release or a package, not both and not neither.");
    }
}

public sealed class StartDeploymentCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver,
    ICurrentUser currentUser,
    ILogger<StartDeploymentCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<StartDeploymentCommand, ObjectIdAndKey>
{
    private const string AppRequestName = nameof(StartDeploymentCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<StartDeploymentCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result<ObjectIdAndKey>> Handle(
        StartDeploymentCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var environment = await _productManagementDbContext.DeploymentEnvironments
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == request.EnvironmentId, cancellationToken);

            if (environment is null)
            {
                _logger.LogInformation("Deployment Environment {EnvironmentId} not found.", request.EnvironmentId);
                return Result.Failure<ObjectIdAndKey>("Deployment environment not found.");
            }

            if (!environment.IsActive)
            {
                return Result.Failure<ObjectIdAndKey>($"'{environment.Name}' is inactive and cannot be deployed into.");
            }

            if (request.ReleaseId is not null
                && !await _productManagementDbContext.Releases
                    .AnyAsync(r => r.Id == request.ReleaseId, cancellationToken))
            {
                return Result.Failure<ObjectIdAndKey>("Release not found.");
            }

            if (request.PackageId is not null
                && !await _productManagementDbContext.ReleasePackages
                    .AnyAsync(p => p.Id == request.PackageId, cancellationToken))
            {
                return Result.Failure<ObjectIdAndKey>("Release package not found.");
            }

            var inProgress = await _statusResolver.ForAlias(
                ProductWorkflowOwners.Deployment.Key,
                scopeId: null,
                (int)ProductStatusAlias.InProgress,
                cancellationToken);

            if (inProgress.IsFailure)
            {
                _logger.LogError("Unable to resolve the in-progress deployment status. Error message: {Error}", inProgress.Error);
                return Result.Failure<ObjectIdAndKey>(inProgress.Error);
            }

            var result = Deployment.Create(
                request.ReleaseId,
                request.PackageId,
                request.EnvironmentId,
                // Frozen from the environment as it stands now, so reclassifying it later cannot rewrite
                // what this deployment counted as.
                environment.Category,
                request.ArtifactId,
                request.StartedAt ?? _dateTimeProvider.Now,
                inProgress.Value,
                environment.Name,
                EventActor.User(_currentUser.GetUserId()),
                _dateTimeProvider.Now);

            if (result.IsFailure)
            {
                _logger.LogInformation("Unable to start a deployment. Error message: {Error}", result.Error);
                return Result.Failure<ObjectIdAndKey>(result.Error);
            }

            await _productManagementDbContext.Deployments.AddAsync(result.Value, cancellationToken);
            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deployment {DeploymentId} started.", result.Value.Id);

            return Result.Success(new ObjectIdAndKey(result.Value.Id, result.Value.Key));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<ObjectIdAndKey>($"Error handling {AppRequestName} command.");
        }
    }
}
