using Wayd.Common.Application.Models;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.ReleasePackages.Commands;

/// <summary>
/// Assembles several component releases into one shipment.
/// </summary>
/// <remarks>
/// The manifest records every component version, changed and carried forward alike, so a reader can
/// reconstruct exactly what was in the box.
/// </remarks>
public sealed record AssembleReleasePackageCommand(
    string Version,
    string? Name,
    LocalDate? TargetDate,
    IReadOnlyCollection<ManifestEntry> Components) : ICommand<ObjectIdAndKey>;

public sealed class AssembleReleasePackageCommandValidator : AbstractValidator<AssembleReleasePackageCommand>
{
    public AssembleReleasePackageCommandValidator()
    {
        RuleFor(p => p.Version)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(p => p.Name)
            .MaximumLength(128);

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

public sealed class AssembleReleasePackageCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver,
    ICurrentUser currentUser,
    ILogger<AssembleReleasePackageCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<AssembleReleasePackageCommand, ObjectIdAndKey>
{
    private const string AppRequestName = nameof(AssembleReleasePackageCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<AssembleReleasePackageCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result<ObjectIdAndKey>> Handle(
        AssembleReleasePackageCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var productIds = request.Components.Select(c => c.ProductId).Distinct().ToList();

            var knownCount = await _productManagementDbContext.Products
                .CountAsync(p => productIds.Contains(p.Id), cancellationToken);

            // A manifest naming a product that does not exist would claim a shipment nobody can trace.
            if (knownCount != productIds.Count)
            {
                return Result.Failure<ObjectIdAndKey>("The manifest names a product that does not exist.");
            }

            // Version ids are optional — a carried-forward component often has no version row — but one
            // that is supplied must resolve. Left unchecked, the projection renders an unknown id as
            // null, making a typo indistinguishable from a legitimately carried-forward component.
            var versionIds = request.Components
                .Where(c => c.VersionId is not null)
                .Select(c => c.VersionId!.Value)
                .Distinct()
                .ToList();

            if (versionIds.Count > 0)
            {
                var knownVersions = await _productManagementDbContext.Versions
                    .CountAsync(v => versionIds.Contains(v.Id), cancellationToken);

                if (knownVersions != versionIds.Count)
                {
                    return Result.Failure<ObjectIdAndKey>("The manifest names a version that does not exist.");
                }
            }

            var initialStatus = await _statusResolver.Initial(
                ProductWorkflowOwners.ReleasePackage.Key, scopeId: null, cancellationToken);

            if (initialStatus.IsFailure)
            {
                _logger.LogError("Unable to resolve the initial package status. Error message: {Error}", initialStatus.Error);
                return Result.Failure<ObjectIdAndKey>(initialStatus.Error);
            }

            var result = ReleasePackage.Create(
                request.Version,
                request.Name,
                request.TargetDate,
                [.. request.Components.Select(c => (c.ProductId, c.VersionId, c.Version, c.Kind))],
                initialStatus.Value,
                EventActor.User(_currentUser.GetUserId()),
                _dateTimeProvider.Now);

            if (result.IsFailure)
            {
                _logger.LogInformation("Unable to assemble a package. Error message: {Error}", result.Error);
                return Result.Failure<ObjectIdAndKey>(result.Error);
            }

            await _productManagementDbContext.ReleasePackages.AddAsync(result.Value, cancellationToken);
            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Release Package {PackageId} assembled.", result.Value.Id);

            return Result.Success(new ObjectIdAndKey(result.Value.Id, result.Value.Key));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<ObjectIdAndKey>($"Error handling {AppRequestName} command.");
        }
    }
}
