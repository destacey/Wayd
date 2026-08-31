using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.ReleasePackages.Commands;

/// <summary>
/// Records that a package shipped.
/// </summary>
/// <remarks>
/// The package is the unit that shipped, so one pipeline run counts once even where it carried several
/// component releases.
/// </remarks>
public sealed record MarkReleasePackageReleasedCommand(Guid Id, LocalDate ReleasedDate) : ICommand, IRequireLinkedEmployee;

public sealed class MarkReleasePackageReleasedCommandValidator : AbstractValidator<MarkReleasePackageReleasedCommand>
{
    public MarkReleasePackageReleasedCommandValidator()
    {
        RuleFor(p => p.Id)
            .NotEmpty();
    }
}

public sealed class MarkReleasePackageReleasedCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    ILogger<MarkReleasePackageReleasedCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<MarkReleasePackageReleasedCommand>
{
    private const string AppRequestName = nameof(MarkReleasePackageReleasedCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<MarkReleasePackageReleasedCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(MarkReleasePackageReleasedCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // The manifest must be loaded: MarkReleased refuses an empty one, and an unloaded
            // collection reads as empty — so without this every release of a real package is refused.
            var package = await _productManagementDbContext.ReleasePackages
                .Include(p => p.Components)
                .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

            if (package is null)
            {
                _logger.LogInformation("Release Package {PackageId} not found.", request.Id);
                return Result.Failure("Release package not found.");
            }

            var status = await _statusResolver.ForAlias(
                ProductWorkflowOwners.ReleasePackage.Key,
                scopeId: null,
                (int)ProductStatusAlias.Released,
                cancellationToken);

            if (status.IsFailure)
            {
                _logger.LogError("Unable to resolve the released package status. Error message: {Error}", status.Error);
                return Result.Failure(status.Error);
            }

            // Read per scope rather than from the claim snapshot, which a personal access token
            // freezes for its whole lifetime. This value is frozen onto the transition, so a stale
            // one would misattribute the change permanently.
            var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);

            var result = package.MarkReleased(
                request.ReleasedDate,
                status.Value,
                EventActor.User(_currentUser.GetUserId(), employeeId),
                _dateTimeProvider.Now);

            if (result.IsFailure)
            {
                package.ClearDomainEvents();

                _logger.LogInformation(
                    "Unable to mark released Release Package {PackageId}. Error message: {Error}", request.Id, result.Error);
                return Result.Failure(result.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Release Package {PackageId} marked released.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
