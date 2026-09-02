namespace Wayd.ProductManagement.Application.Releases.Commands;

/// <summary>
/// Replaces the packages a release shipped.
/// </summary>
/// <remarks>
/// Whole-set replacement, as with the versions: a package left out of the request is removed from the
/// release.
/// <para>
/// A package may serve more than one release — the same weekly shipment can carry work announced
/// under two product lines — so this adds a membership rather than claiming the package.
/// </para>
/// </remarks>
public sealed record SetReleasePackagesCommand(Guid Id, IReadOnlyCollection<Guid> PackageIds)
    : ICommand, IRequireLinkedEmployee;

public sealed class SetReleasePackagesCommandValidator : AbstractValidator<SetReleasePackagesCommand>
{
    public SetReleasePackagesCommandValidator()
    {
        RuleFor(r => r.Id)
            .NotEmpty();

        RuleFor(r => r.PackageIds)
            .NotNull();
    }
}

public sealed class SetReleasePackagesCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    ILogger<SetReleasePackagesCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<SetReleasePackagesCommand>
{
    private const string AppRequestName = nameof(SetReleasePackagesCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<SetReleasePackagesCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(SetReleasePackagesCommand request, CancellationToken cancellationToken)
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

            var packageIds = request.PackageIds.Distinct().ToList();

            if (packageIds.Count > 0)
            {
                var known = await _productManagementDbContext.ReleasePackages
                    .CountAsync(p => packageIds.Contains(p.Id), cancellationToken);

                if (known != packageIds.Count)
                {
                    return Result.Failure("The release names a package that does not exist.");
                }
            }

            // Resolved from the packages being set rather than the ones already attached: the rule is
            // about what this release would contain afterwards, so checking the current set would let
            // a newly-added package duplicate a directly-carried version.
            var versionIdsInPackages = await _productManagementDbContext.ReleasePackageComponents
                .Where(c => packageIds.Contains(c.PackageId) && c.VersionId != null)
                .Select(c => c.VersionId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);

            var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);

            var result = release.ShipPackages(
                packageIds,
                versionIdsInPackages,
                EventActor.User(_currentUser.GetUserId(), employeeId),
                _dateTimeProvider.Now);

            if (result.IsFailure)
            {
                release.ClearDomainEvents();

                _logger.LogInformation(
                    "Unable to set Release {ReleaseId} packages. Error message: {Error}", request.Id, result.Error);
                return Result.Failure(result.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Release {ReleaseId} packages set.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
