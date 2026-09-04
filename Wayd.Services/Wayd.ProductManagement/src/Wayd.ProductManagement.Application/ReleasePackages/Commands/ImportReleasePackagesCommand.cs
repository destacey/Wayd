using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.ProductManagement.Application.ReleasePackages.Dtos;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.ReleasePackages.Commands;

/// <summary>
/// Additively imports a batch of release packages, each assembled with its manifest and then marked
/// released where the row says it shipped.
/// <para>
/// The manifest arrives with the package rather than afterwards, because a package cannot exist
/// without one — an empty manifest says nothing about what shipped, and the aggregate refuses it. That
/// is why the import takes two files: one row per package, and one row per manifest line pointing back
/// at its package by version.
/// </para>
/// <para>
/// A manifest line names a version by string. Where a matching version record exists for that product
/// the line is linked to it; where none does the string stands alone, which is the carried-forward
/// case the domain models deliberately — a component that was already running and was never cut here.
/// An unmatched string is therefore <em>not</em> an error.
/// </para>
/// <para>
/// The batch is all-or-nothing: products are resolved by name, and any package version duplicated
/// within the file, any manifest line pointing at no package, any unresolved or ambiguous product, and
/// any component appearing twice in one manifest fails the whole import.
/// </para>
/// </summary>
public sealed record ImportReleasePackagesCommand : ICommand
{
    public ImportReleasePackagesCommand(IEnumerable<ImportReleasePackageDto> packages)
    {
        Packages = [.. packages];
    }

    public List<ImportReleasePackageDto> Packages { get; }
}

public sealed class ImportReleasePackagesCommandValidator : AbstractValidator<ImportReleasePackagesCommand>
{
    public ImportReleasePackagesCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(p => p.Packages)
            .NotNull()
            .NotEmpty();

        RuleForEach(p => p.Packages)
            .NotNull()
            .SetValidator(new ImportReleasePackageDtoValidator());
    }
}

public sealed class ImportReleasePackagesCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver,
    ICurrentUser currentUser,
    ILogger<ImportReleasePackagesCommandHandler> logger,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<ImportReleasePackagesCommand>
{
    private const string AppRequestName = nameof(ImportReleasePackagesCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<ImportReleasePackagesCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(ImportReleasePackagesCommand request, CancellationToken cancellationToken)
    {
        var timestamp = _dateTimeProvider.Now;

        // One import run is one actor: the events say "the import", not "this person assembled every
        // package by hand", while still recording who set it running.
        var actor = EventActor.Import(_currentUser.GetUserId());

        try
        {
            var duplicates = request.Packages
                .GroupBy(p => Normalize(p.Version), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (duplicates.Count > 0)
                return Fail($"The following package versions appear more than once in the import: {Quote(duplicates)}.");

            var versions = request.Packages.Select(p => Normalize(p.Version)).ToList();

            var existing = await _productManagementDbContext.ReleasePackages
                .Where(p => versions.Contains(p.Version))
                .Select(p => p.Version)
                .ToListAsync(cancellationToken);
            if (existing.Count > 0)
                return Fail($"The following release packages already exist: {Quote(existing)}.");

            var productsResult = await ResolveProducts(request, cancellationToken);
            if (productsResult.IsFailure)
                return Result.Failure(productsResult.Error);
            var productIdsByName = productsResult.Value;

            var versionIds = await ResolveComponentVersions(request, productIdsByName, cancellationToken);

            var statusesResult = await ResolveStatuses(cancellationToken);
            if (statusesResult.IsFailure)
                return Result.Failure(statusesResult.Error);
            var (initialStatus, releasedStatus) = statusesResult.Value;

            foreach (var row in request.Packages)
            {
                // A miss must stay null rather than becoming Guid.Empty: the nullable VersionId is
                // what records a carried-forward component whose version was never cut here.
                var components = row.Components
                    .Select(c => (
                        ProductId: productIdsByName[Normalize(c.ProductName)],
                        VersionId: versionIds.TryGetValue(ComponentKey(c.ProductName, c.VersionNumber), out var versionId)
                            ? versionId
                            : (Guid?)null,
                        Version: Normalize(c.VersionNumber),
                        c.Kind))
                    .ToList();

                var createResult = ReleasePackage.Create(
                    Normalize(row.Version),
                    row.Name,
                    row.TargetDate,
                    components,
                    initialStatus,
                    actor,
                    timestamp);

                if (createResult.IsFailure)
                    return Fail($"Could not assemble package '{row.Version}': {createResult.Error}");

                var package = createResult.Value;

                if (row.ReleasedDate is not null)
                {
                    var releaseResult = package.MarkReleased(
                        row.ReleasedDate.Value, releasedStatus, actor, timestamp);

                    if (releaseResult.IsFailure)
                        return Fail($"Could not release package '{row.Version}': {releaseResult.Error}");
                }

                await _productManagementDbContext.ReleasePackages.AddAsync(package, cancellationToken);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("{AppRequestName}: imported {Count} release package(s).", AppRequestName, request.Packages.Count);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception for request {AppRequestName}", AppRequestName);

            return Result.Failure($"Exception for request {AppRequestName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Resolves every product named by a manifest line.
    /// </summary>
    /// <remarks>
    /// Product names carry no unique index, so an ambiguous name fails the batch rather than guessing
    /// which product a manifest line describes. Releasability is deliberately <em>not</em> checked: a
    /// manifest records what was in the box, and a carried-forward component may be a node no version
    /// was ever cut against.
    /// </remarks>
    private async Task<Result<Dictionary<string, Guid>>> ResolveProducts(
        ImportReleasePackagesCommand request, CancellationToken cancellationToken)
    {
        var productNames = request.Packages
            .SelectMany(p => p.Components)
            .Select(c => Normalize(c.ProductName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var products = await _productManagementDbContext.Products
            .Where(p => productNames.Contains(p.Name))
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(cancellationToken);

        var ambiguous = products
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (ambiguous.Count > 0)
        {
            return Fail<Dictionary<string, Guid>>(
                $"The following product names match more than one product: {Quote(ambiguous)}.");
        }

        var productIdsByName = products.ToDictionary(p => p.Name, p => p.Id, StringComparer.OrdinalIgnoreCase);

        var unresolved = productNames.Where(n => !productIdsByName.ContainsKey(n)).ToList();
        if (unresolved.Count > 0)
            return Fail<Dictionary<string, Guid>>($"Could not resolve the following products: {Quote(unresolved)}.");

        return Result.Success(productIdsByName);
    }

    /// <summary>
    /// Links manifest lines to version records where one matches, keyed by product and number.
    /// </summary>
    /// <remarks>
    /// A miss is not a failure. A carried-forward component frequently names a version that was never
    /// cut in Wayd, and recording the string without a link is exactly what lets a manifest answer
    /// "what was running" for every component rather than only the changed ones.
    /// </remarks>
    private async Task<Dictionary<string, Guid>> ResolveComponentVersions(
        ImportReleasePackagesCommand request,
        Dictionary<string, Guid> productIdsByName,
        CancellationToken cancellationToken)
    {
        var productIds = productIdsByName.Values.ToList();

        var versions = await _productManagementDbContext.Versions
            .Where(v => productIds.Contains(v.ProductId))
            .Select(v => new { v.Id, v.ProductId, v.Number })
            .ToListAsync(cancellationToken);

        if (versions.Count == 0)
            return new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        var namesByProductId = productIdsByName.ToDictionary(pair => pair.Value, pair => pair.Key);

        // A product may hold the same number only once, so the first match is the only match.
        return versions
            .Where(v => namesByProductId.ContainsKey(v.ProductId))
            .GroupBy(v => ComponentKey(namesByProductId[v.ProductId], v.Number), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolves the two statuses an imported package can hold, once for the whole batch.
    /// </summary>
    private async Task<Result<(StatusRef Initial, StatusRef Released)>> ResolveStatuses(
        CancellationToken cancellationToken)
    {
        // Product Management assigns workflows organization-wide, so the scope is null.
        var initial = await _statusResolver.Initial(
            ProductWorkflowOwners.ReleasePackage.Key, scopeId: null, cancellationToken);
        if (initial.IsFailure)
        {
            _logger.LogError("Unable to resolve the initial package status. Error message: {Error}", initial.Error);
            return Result.Failure<(StatusRef, StatusRef)>(initial.Error);
        }

        var released = await _statusResolver.ForAlias(
            ProductWorkflowOwners.ReleasePackage.Key, scopeId: null, (int)ProductStatusAlias.Released, cancellationToken);
        if (released.IsFailure)
        {
            _logger.LogError("Unable to resolve the released package status. Error message: {Error}", released.Error);
            return Result.Failure<(StatusRef, StatusRef)>(released.Error);
        }

        return Result.Success((initial.Value, released.Value));
    }

    /// <summary>Identifies a component version: a number is only unique within its product.</summary>
    private static string ComponentKey(string productName, string versionNumber) =>
        $"{Normalize(productName)} {Normalize(versionNumber)}";

    private Result Fail(string message)
    {
        _logger.LogWarning("{AppRequestName}: {Message}", AppRequestName, message);
        return Result.Failure(message);
    }

    private Result<T> Fail<T>(string message)
    {
        _logger.LogWarning("{AppRequestName}: {Message}", AppRequestName, message);
        return Result.Failure<T>(message);
    }

    private static string Normalize(string value) => value.Trim();

    private static string Quote(IEnumerable<string> values) => string.Join(", ", values.Select(v => $"'{v}'"));
}
