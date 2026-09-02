namespace Wayd.ProductManagement.Application.Releases.Commands;

/// <summary>
/// Replaces the versions a release carries directly, outside any package.
/// </summary>
/// <remarks>
/// Whole-set replacement, matching the package manifest: a version left out of the request is removed
/// from the release. A partially-applied change would claim a combination that was never announced.
/// <para>
/// This is the single-artifact route. Most contents run through a package, which is the deployment
/// unit; a release announcing one artifact on its own carries it here rather than inventing a package
/// of one.
/// </para>
/// </remarks>
public sealed record SetReleaseVersionsCommand(Guid Id, IReadOnlyCollection<Guid> VersionIds)
    : ICommand, IRequireLinkedEmployee;

public sealed class SetReleaseVersionsCommandValidator : AbstractValidator<SetReleaseVersionsCommand>
{
    public SetReleaseVersionsCommandValidator()
    {
        RuleFor(r => r.Id)
            .NotEmpty();

        RuleFor(r => r.VersionIds)
            .NotNull();
    }
}

public sealed class SetReleaseVersionsCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    ILogger<SetReleaseVersionsCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<SetReleaseVersionsCommand>
{
    private const string AppRequestName = nameof(SetReleaseVersionsCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<SetReleaseVersionsCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(SetReleaseVersionsCommand request, CancellationToken cancellationToken)
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

            var versionIds = request.VersionIds.Distinct().ToList();

            if (versionIds.Count > 0)
            {
                var known = await _productManagementDbContext.Versions
                    .CountAsync(v => versionIds.Contains(v.Id), cancellationToken);

                if (known != versionIds.Count)
                {
                    return Result.Failure("The release names a version that does not exist.");
                }
            }

            // Every version reachable through this release's packages. The aggregate enforces that a
            // version cannot be carried directly as well, but cannot load a manifest to find out.
            var packageIds = release.Packages.Select(p => p.PackageId).ToList();
            var versionIdsInPackages = await _productManagementDbContext.ReleasePackageComponents
                .Where(c => packageIds.Contains(c.PackageId) && c.VersionId != null)
                .Select(c => c.VersionId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);

            var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);

            var result = release.CarryVersions(
                versionIds,
                versionIdsInPackages,
                EventActor.User(_currentUser.GetUserId(), employeeId),
                _dateTimeProvider.Now);

            if (result.IsFailure)
            {
                release.ClearDomainEvents();

                _logger.LogInformation(
                    "Unable to set Release {ReleaseId} versions. Error message: {Error}", request.Id, result.Error);
                return Result.Failure(result.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Release {ReleaseId} versions set.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
