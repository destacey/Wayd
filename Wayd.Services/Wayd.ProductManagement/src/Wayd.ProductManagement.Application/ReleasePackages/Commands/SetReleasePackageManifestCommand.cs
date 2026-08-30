namespace Wayd.ProductManagement.Application.ReleasePackages.Commands;

/// <summary>
/// Replaces a package's manifest wholesale.
/// </summary>
/// <remarks>
/// Whole-manifest replacement, never incremental: a partially-updated manifest would claim a set of
/// versions that never shipped together.
/// </remarks>
public sealed record SetReleasePackageManifestCommand(
    Guid Id,
    IReadOnlyCollection<ManifestEntry> Components) : ICommand;

public sealed class SetReleasePackageManifestCommandValidator
    : AbstractValidator<SetReleasePackageManifestCommand>
{
    public SetReleasePackageManifestCommandValidator()
    {
        RuleFor(p => p.Id)
            .NotEmpty();

        RuleFor(p => p.Components)
            .NotEmpty()
            .WithMessage("A package must be assembled from at least one component.");

        RuleForEach(p => p.Components).ChildRules(c =>
        {
            c.RuleFor(e => e.ProductId).NotEmpty();
            c.RuleFor(e => e.Version).NotEmpty().MaximumLength(64);
            c.RuleFor(e => e.Kind).IsInEnum();
        });
    }
}

public sealed class SetReleasePackageManifestCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    ILogger<SetReleasePackageManifestCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<SetReleasePackageManifestCommand>
{
    private const string AppRequestName = nameof(SetReleasePackageManifestCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<SetReleasePackageManifestCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(SetReleasePackageManifestCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // The current manifest must be loaded: SetManifest compares against it to decide whether
            // anything actually changed, and an unloaded collection would read as empty.
            var package = await _productManagementDbContext.ReleasePackages
                .Include(p => p.Components)
                .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

            if (package is null)
            {
                _logger.LogInformation("Release Package {PackageId} not found.", request.Id);
                return Result.Failure("Release package not found.");
            }

            var productIds = request.Components.Select(c => c.ProductId).Distinct().ToList();

            var knownCount = await _productManagementDbContext.Products
                .CountAsync(p => productIds.Contains(p.Id), cancellationToken);

            if (knownCount != productIds.Count)
            {
                return Result.Failure("The manifest names a product that does not exist.");
            }

            var result = package.SetManifest(
                [.. request.Components.Select(c => (c.ProductId, c.ReleaseId, c.Version, c.Kind))],
                EventActor.User(_currentUser.GetUserId()),
                _dateTimeProvider.Now);

            if (result.IsFailure)
            {
                package.ClearDomainEvents();

                _logger.LogInformation(
                    "Unable to set the manifest for {PackageId}. Error message: {Error}", request.Id, result.Error);
                return Result.Failure(result.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Release Package {PackageId} manifest set.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
