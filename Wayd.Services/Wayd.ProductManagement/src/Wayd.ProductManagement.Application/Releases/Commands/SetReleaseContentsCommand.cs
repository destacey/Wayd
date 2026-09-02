namespace Wayd.ProductManagement.Application.Releases.Commands;

/// <summary>
/// Replaces everything a release announces — the packages it shipped and the versions it carries
/// directly — as one set.
/// </summary>
/// <remarks>
/// Whole-set replacement, matching the package manifest: anything left out of the request is removed
/// from the release. Both lists empty clears it, which is a legitimate state — a repackaging or a
/// pricing change is announced with nothing deployed.
/// <para>
/// Both routes are set in one command because the rule that a version is announced once spans them.
/// Two commands would have to judge that rule against different baselines, and moving a version into
/// the package that carries it would depend on which half was sent first.
/// </para>
/// <para>
/// A package may serve more than one release — the same weekly shipment can carry work announced under
/// two product lines — so this adds a membership rather than claiming the package.
/// </para>
/// </remarks>
public sealed record SetReleaseContentsCommand(
    Guid Id,
    IReadOnlyCollection<Guid> VersionIds,
    IReadOnlyCollection<Guid> PackageIds)
    : ICommand, IRequireLinkedEmployee;

public sealed class SetReleaseContentsCommandValidator : AbstractValidator<SetReleaseContentsCommand>
{
    public SetReleaseContentsCommandValidator()
    {
        RuleFor(r => r.Id)
            .NotEmpty();

        RuleFor(r => r.VersionIds)
            .NotNull();

        RuleFor(r => r.PackageIds)
            .NotNull();
    }
}

public sealed class SetReleaseContentsCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    ILogger<SetReleaseContentsCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<SetReleaseContentsCommand>
{
    private const string AppRequestName = nameof(SetReleaseContentsCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<SetReleaseContentsCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(SetReleaseContentsCommand request, CancellationToken cancellationToken)
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
            var packageIds = request.PackageIds.Distinct().ToList();

            if (versionIds.Count > 0)
            {
                var known = await _productManagementDbContext.Versions
                    .CountAsync(v => versionIds.Contains(v.Id), cancellationToken);

                if (known != versionIds.Count)
                {
                    return Result.Failure("The release names a version that does not exist.");
                }
            }

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
            // about what this release contains afterwards. A manifest line that names no version record
            // — a component carried forward from before Wayd held it — covers nothing, so it cannot
            // conflict with a version carried directly.
            var versionIdsInPackages = await _productManagementDbContext.ReleasePackageComponents
                .Where(c => packageIds.Contains(c.PackageId) && c.VersionId != null)
                .Select(c => c.VersionId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);

            var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);

            var result = release.SetContents(
                versionIds,
                packageIds,
                versionIdsInPackages,
                EventActor.User(_currentUser.GetUserId(), employeeId),
                _dateTimeProvider.Now);

            if (result.IsFailure)
            {
                release.ClearDomainEvents();

                _logger.LogInformation(
                    "Unable to set Release {ReleaseId} contents. Error message: {Error}", request.Id, result.Error);
                return Result.Failure(result.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Release {ReleaseId} contents set.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
