using Wayd.ProductManagement.Application.Deployments;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.ReleasePackages.Commands;

/// <summary>
/// Permanently deletes a release package with its manifest, its status history and every deployment
/// of it.
/// </summary>
/// <remarks>
/// Refused while any release lists it: a delete never edits a release behind its back, and a released
/// release's contents are the record of what shipped. The versions its manifest names are separate
/// records and stay. Withdrawing keeps everything, and is the everyday way out; this is for a package
/// assembled by mistake, or for purging history.
/// </remarks>
public sealed record DeleteReleasePackageCommand(Guid Id) : ICommand;

public sealed class DeleteReleasePackageCommandValidator : AbstractValidator<DeleteReleasePackageCommand>
{
    public DeleteReleasePackageCommandValidator()
    {
        RuleFor(p => p.Id)
            .NotEmpty();
    }
}

public sealed class DeleteReleasePackageCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusWorkflowDbContext statusWorkflowDbContext,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    ILogger<DeleteReleasePackageCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<DeleteReleasePackageCommand>
{
    private const string AppRequestName = nameof(DeleteReleasePackageCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusWorkflowDbContext _statusWorkflowDbContext = statusWorkflowDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<DeleteReleasePackageCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(DeleteReleasePackageCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Components cascade in the database, but are loaded so the tracked graph matches.
            var package = await _productManagementDbContext.ReleasePackages
                .Include(p => p.Components)
                .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

            if (package is null)
            {
                _logger.LogInformation("Release Package {PackageId} not found.", request.Id);
                return Result.Failure("Release package not found.");
            }

            var releaseCount = await _productManagementDbContext.ReleasePackageInclusions
                .CountAsync(i => i.PackageId == request.Id, cancellationToken);

            if (releaseCount > 0)
            {
                return Result.Failure(
                    $"This package is listed in {releaseCount} release(s). Remove it from them, or delete them, first.");
            }

            var deployments = await _productManagementDbContext.Deployments
                .Where(d => d.PackageId == request.Id)
                .ToListAsync(cancellationToken);

            var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
            var actor = EventActor.User(_currentUser.GetUserId(), employeeId);
            var timestamp = _dateTimeProvider.Now;

            // Deployments restrict on the package, so they go in the same save rather than being left to
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
                ProductWorkflowOwners.ReleasePackage.Key,
                [package.Id],
                cancellationToken);

            package.Delete(actor, timestamp);
            _productManagementDbContext.ReleasePackages.Remove(package);

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Release Package {PackageId} deleted with {DeploymentCount} deployments.",
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
