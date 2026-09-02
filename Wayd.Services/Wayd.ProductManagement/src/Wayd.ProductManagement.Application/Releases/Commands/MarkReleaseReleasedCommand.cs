using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.Releases.Commands;

/// <summary>
/// Records that a release was announced to customers.
/// </summary>
/// <remarks>
/// The released date is what orders a release history, so it is supplied rather than taken from the
/// clock: announcing is often recorded after the fact.
/// </remarks>
public sealed record MarkReleaseReleasedCommand(Guid Id, LocalDate ReleasedDate) : ICommand, IRequireLinkedEmployee;

public sealed class MarkReleaseReleasedCommandValidator : AbstractValidator<MarkReleaseReleasedCommand>
{
    public MarkReleaseReleasedCommandValidator()
    {
        RuleFor(r => r.Id)
            .NotEmpty();
    }
}

public sealed class MarkReleaseReleasedCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    ILogger<MarkReleaseReleasedCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<MarkReleaseReleasedCommand>
{
    private const string AppRequestName = nameof(MarkReleaseReleasedCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<MarkReleaseReleasedCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(MarkReleaseReleasedCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var release = await _productManagementDbContext.Releases
                .Include(r => r.Versions)
                .Include(r => r.Packages)
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

            if (release is null)
            {
                _logger.LogInformation("Release {ReleaseId} not found.", request.Id);
                return Result.Failure("Release not found.");
            }

            // The aggregate demands the status carrying this meaning and cannot fetch it. Resolving by
            // alias rather than by id is what lets an organization rename or reorder its workflow
            // without breaking the transition.
            var status = await _statusResolver.ForAlias(
                ProductWorkflowOwners.Release.Key,
                scopeId: null,
                (int)ProductStatusAlias.Released,
                cancellationToken);

            if (status.IsFailure)
            {
                _logger.LogError("Unable to resolve the released release status. Error message: {Error}", status.Error);
                return Result.Failure(status.Error);
            }

            // Whether anything this release carries has yet to ship. The aggregate holds ids, not the
            // records, so it cannot answer this itself — but it is the one claim an announcement makes
            // that its own contents can contradict, so it is checked rather than assumed.
            var versionIds = release.Versions.Select(v => v.VersionId).ToList();
            var packageIds = release.Packages.Select(p => p.PackageId).ToList();

            var hasUnreleasedVersion = versionIds.Count > 0
                && await _productManagementDbContext.Versions
                    .AnyAsync(v => versionIds.Contains(v.Id) && v.ReleasedDate == null, cancellationToken);

            var hasUnreleasedPackage = packageIds.Count > 0
                && await _productManagementDbContext.ReleasePackages
                    .AnyAsync(p => packageIds.Contains(p.Id) && p.ReleasedDate == null, cancellationToken);

            var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);

            var result = release.MarkReleased(
                request.ReleasedDate,
                hasUnreleasedVersion || hasUnreleasedPackage,
                status.Value,
                EventActor.User(_currentUser.GetUserId(), employeeId),
                _dateTimeProvider.Now);

            if (result.IsFailure)
            {
                release.ClearDomainEvents();

                _logger.LogInformation(
                    "Unable to release Release {ReleaseId}. Error message: {Error}", request.Id, result.Error);
                return Result.Failure(result.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Release {ReleaseId} released.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
