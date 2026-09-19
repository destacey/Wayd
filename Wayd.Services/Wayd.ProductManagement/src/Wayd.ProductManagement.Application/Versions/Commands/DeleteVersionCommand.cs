using Wayd.ProductManagement.Application.Deployments;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.Versions.Commands;

/// <summary>
/// Permanently deletes a version with its status history and every deployment of it.
/// </summary>
/// <remarks>
/// Refused while a release lists it or a package manifest names it: a delete never edits another record
/// behind its back, and an announced release's contents, like a released package's manifest, are the
/// record of what shipped. Withdrawing keeps everything, and is the everyday way out; this is for a
/// version recorded by mistake, or for purging history.
/// </remarks>
public sealed record DeleteVersionCommand(Guid Id) : ICommand;

public sealed class DeleteVersionCommandValidator : AbstractValidator<DeleteVersionCommand>
{
    public DeleteVersionCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();
    }
}

public sealed class DeleteVersionCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusWorkflowDbContext statusWorkflowDbContext,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    ILogger<DeleteVersionCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<DeleteVersionCommand>
{
    private const string AppRequestName = nameof(DeleteVersionCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusWorkflowDbContext _statusWorkflowDbContext = statusWorkflowDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<DeleteVersionCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(DeleteVersionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var version = await _productManagementDbContext.Versions
                .FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken);

            if (version is null)
            {
                _logger.LogInformation("Version {VersionId} not found.", request.Id);
                return Result.Failure("Version not found.");
            }

            var releaseCount = await _productManagementDbContext.ReleaseVersions
                .CountAsync(rv => rv.VersionId == request.Id, cancellationToken);

            if (releaseCount > 0)
            {
                return Result.Failure(
                    $"This version is listed in {releaseCount} release(s). Remove it from them, or delete them, first.");
            }

            // A manifest line holds the version by id without a foreign key, so nothing would stop the
            // delete — it would leave the package pointing at a version that no longer exists.
            var packageCount = await _productManagementDbContext.ReleasePackageComponents
                .Where(c => c.VersionId == request.Id)
                .Select(c => c.PackageId)
                .Distinct()
                .CountAsync(cancellationToken);

            if (packageCount > 0)
            {
                return Result.Failure(
                    $"This version is named in the manifest of {packageCount} package(s). Remove it from them, or delete them, first.");
            }

            var deployments = await _productManagementDbContext.Deployments
                .Where(d => d.VersionId == request.Id)
                .ToListAsync(cancellationToken);

            var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
            var actor = EventActor.User(_currentUser.GetUserId(), employeeId);
            var timestamp = _dateTimeProvider.Now;

            // Deployments restrict on the version, so they go in the same save rather than being left to
            // fail the delete at the database.
            await DeploymentRemoval.Stage(
                _productManagementDbContext,
                _statusWorkflowDbContext,
                deployments,
                actor,
                timestamp,
                cancellationToken);

            await StatusHistoryRemoval.Stage(
                _statusWorkflowDbContext,
                ProductWorkflowOwners.Version.Key,
                [version.Id],
                cancellationToken);

            version.Delete(actor, timestamp);
            _productManagementDbContext.Versions.Remove(version);

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Version {VersionId} deleted with {DeploymentCount} deployments.",
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
