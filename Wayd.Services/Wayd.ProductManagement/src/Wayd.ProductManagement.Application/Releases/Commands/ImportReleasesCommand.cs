using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.ProductManagement.Application.Releases.Dtos;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Releases.Commands;

/// <summary>
/// Additively imports a batch of releases, each planned, given its contents, and then announced where
/// the row says it was.
/// <para>
/// The three steps run in that order because the domain requires it: contents cannot be amended once a
/// release is announced, so a release that ships must receive them first. This is the one importer in
/// the module whose steps are genuinely ordered rather than merely convenient.
/// </para>
/// <para>
/// It is also the only one with a cross-record precondition. A release refuses to be announced while
/// anything it carries has not shipped — the one claim a release can make that its own contents
/// contradict. A row saying a release was announced while one of its versions has no released date
/// therefore fails the batch rather than importing the release unannounced, because quietly demoting
/// it would record something the file did not say.
/// </para>
/// <para>
/// The batch is all-or-nothing: releases are keyed by version alone (a release's product is optional
/// by design), and any duplicate, unresolved reference, or content announced twice fails the import.
/// </para>
/// </summary>
public sealed record ImportReleasesCommand : ICommand
{
    public ImportReleasesCommand(IEnumerable<ImportReleaseDto> releases)
    {
        Releases = [.. releases];
    }

    public List<ImportReleaseDto> Releases { get; }
}

public sealed class ImportReleasesCommandValidator : AbstractValidator<ImportReleasesCommand>
{
    public ImportReleasesCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Releases)
            .NotNull()
            .NotEmpty();

        RuleForEach(r => r.Releases)
            .NotNull()
            .SetValidator(new ImportReleaseDtoValidator());
    }
}

public sealed class ImportReleasesCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver,
    ICurrentUser currentUser,
    ILogger<ImportReleasesCommandHandler> logger,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<ImportReleasesCommand>
{
    private const string AppRequestName = nameof(ImportReleasesCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<ImportReleasesCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(ImportReleasesCommand request, CancellationToken cancellationToken)
    {
        var timestamp = _dateTimeProvider.Now;

        // One import run is one actor: the events say "the import", not "this person announced every
        // release by hand", while still recording who set it running.
        var actor = EventActor.Import(_currentUser.GetUserId());

        try
        {
            var duplicates = request.Releases
                .GroupBy(r => Normalize(r.Version), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (duplicates.Count > 0)
                return Fail($"The following release versions appear more than once in the import: {Quote(duplicates)}.");

            var versionLabels = request.Releases.Select(r => Normalize(r.Version)).ToList();

            var existing = await _productManagementDbContext.Releases
                .Where(r => versionLabels.Contains(r.Version))
                .Select(r => r.Version)
                .ToListAsync(cancellationToken);
            if (existing.Count > 0)
                return Fail($"The following releases already exist: {Quote(existing)}.");

            var productsResult = await ResolveProducts(request, cancellationToken);
            if (productsResult.IsFailure)
                return Result.Failure(productsResult.Error);
            var productIdsByName = productsResult.Value;

            var contentsResult = await ResolveContents(request, cancellationToken);
            if (contentsResult.IsFailure)
                return Result.Failure(contentsResult.Error);
            var contents = contentsResult.Value;

            var statusesResult = await ResolveStatuses(cancellationToken);
            if (statusesResult.IsFailure)
                return Result.Failure(statusesResult.Error);
            var (initialStatus, releasedStatus) = statusesResult.Value;

            foreach (var row in request.Releases)
            {
                Guid? productId = string.IsNullOrWhiteSpace(row.ProductName)
                    ? null
                    : productIdsByName[Normalize(row.ProductName)];

                var createResult = Release.Create(
                    productId,
                    Normalize(row.Version),
                    row.Name,
                    row.TargetDate,
                    row.Sequence,
                    initialStatus,
                    actor,
                    timestamp);

                if (createResult.IsFailure)
                    return Fail($"Could not plan release '{row.Version}': {createResult.Error}");

                var release = createResult.Value;

                var applyResult = ApplyContents(release, row, contents, actor, timestamp);
                if (applyResult.IsFailure)
                    return Result.Failure(applyResult.Error);

                if (!string.IsNullOrWhiteSpace(row.Notes))
                {
                    var notesResult = release.UpdateDetails(
                        Normalize(row.Version), row.Name, row.Notes, productId, row.Sequence, actor, timestamp);

                    if (notesResult.IsFailure)
                        return Fail($"Could not set notes on release '{row.Version}': {notesResult.Error}");
                }

                if (row.ReleasedDate is not null)
                {
                    var announceResult = Announce(release, row, contents, releasedStatus, actor, timestamp);
                    if (announceResult.IsFailure)
                        return Result.Failure(announceResult.Error);
                }

                await _productManagementDbContext.Releases.AddAsync(release, cancellationToken);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("{AppRequestName}: imported {Count} release(s).", AppRequestName, request.Releases.Count);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception for request {AppRequestName}", AppRequestName);

            return Result.Failure($"Exception for request {AppRequestName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Sets what a release announces, before it can be announced.
    /// </summary>
    /// <remarks>
    /// Both routes go in one call because the invariant spans them: a version shipping inside one of
    /// the release's packages must not also be carried directly, or one shipment would be announced
    /// twice. The aggregate cannot load a package's manifest, so the versions reachable through the
    /// chosen packages are resolved here and handed in.
    /// </remarks>
    private Result ApplyContents(
        Release release,
        ImportReleaseDto row,
        ResolvedContents contents,
        EventActor actor,
        Instant timestamp)
    {
        var rows = contents.ByRelease.GetValueOrDefault(Normalize(row.Version), []);

        if (rows.Count == 0)
            return Result.Success();

        var packageIds = rows
            .Where(c => c.Kind == ReleaseContentKind.Package)
            .Select(c => contents.PackageIdsByVersion[Normalize(c.PackageVersion!)])
            .ToList();

        var versionIds = rows
            .Where(c => c.Kind == ReleaseContentKind.Version)
            .Select(c => contents.VersionIdsByKey[VersionKey(c.ProductName!, c.VersionNumber!)])
            .ToList();

        var versionIdsInPackages = packageIds
            .SelectMany(id => contents.VersionIdsByPackageId.GetValueOrDefault(id, []))
            .Distinct()
            .ToList();

        var result = release.SetContents(versionIds, packageIds, versionIdsInPackages, actor, timestamp);

        return result.IsFailure
            ? Fail($"Could not set contents on release '{row.Version}': {result.Error}")
            : Result.Success();
    }

    /// <summary>
    /// Announces a release, refusing where anything it carries has not shipped.
    /// </summary>
    /// <remarks>
    /// The aggregate holds ids rather than records, so whether its contents have shipped is resolved
    /// here. The refusal names what is holding the release back: without that, the message says only
    /// that something is unshipped and leaves the reader to find it.
    /// </remarks>
    private Result Announce(
        Release release,
        ImportReleaseDto row,
        ResolvedContents contents,
        StatusRef releasedStatus,
        EventActor actor,
        Instant timestamp)
    {
        var unshipped = release.Versions
            .Select(v => v.VersionId)
            .Where(id => !contents.ReleasedVersionIds.Contains(id))
            .Select(id => contents.VersionLabelsById.GetValueOrDefault(id, id.ToString()))
            .Concat(release.Packages
                .Select(p => p.PackageId)
                .Where(id => !contents.ReleasedPackageIds.Contains(id))
                .Select(id => contents.PackageLabelsById.GetValueOrDefault(id, id.ToString())))
            .ToList();

        if (unshipped.Count > 0)
        {
            return Fail(
                $"Release '{row.Version}' is marked as released but carries content that has not shipped: {Quote(unshipped)}. "
                + "Import those with a released date first, or leave this release's ReleasedDate empty.");
        }

        var result = release.MarkReleased(
            row.ReleasedDate!.Value, hasUnreleasedContents: false, releasedStatus, actor, timestamp);

        return result.IsFailure
            ? Fail($"Could not release '{row.Version}': {result.Error}")
            : Result.Success();
    }

    /// <summary>
    /// Resolves the products releases name. Optional, so only the rows that name one are looked up.
    /// </summary>
    private async Task<Result<Dictionary<string, Guid>>> ResolveProducts(
        ImportReleasesCommand request, CancellationToken cancellationToken)
    {
        var productNames = request.Releases
            .Where(r => !string.IsNullOrWhiteSpace(r.ProductName))
            .Select(r => Normalize(r.ProductName!))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (productNames.Count == 0)
            return Result.Success(new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase));

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
    /// Resolves every package and version the batch announces, along with what has shipped and what
    /// each package carries.
    /// </summary>
    /// <remarks>
    /// Unlike a package manifest — where an unmatched version string is the carried-forward case and
    /// perfectly legitimate — a release's contents must resolve. A release announcing something that
    /// does not exist in Wayd is a mistyped reference, not a record of history.
    /// </remarks>
    private async Task<Result<ResolvedContents>> ResolveContents(
        ImportReleasesCommand request, CancellationToken cancellationToken)
    {
        var rows = request.Releases.SelectMany(r => r.Contents).ToList();

        var byRelease = rows
            .GroupBy(c => Normalize(c.ReleaseVersion), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ImportReleaseContentDto>)[.. g],
                StringComparer.OrdinalIgnoreCase);

        var packageVersions = rows
            .Where(c => c.Kind == ReleaseContentKind.Package)
            .Select(c => Normalize(c.PackageVersion!))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var packages = packageVersions.Count == 0
            ? []
            : await _productManagementDbContext.ReleasePackages
                .Include(p => p.Components)
                .Where(p => packageVersions.Contains(p.Version))
                .ToListAsync(cancellationToken);

        var ambiguousPackages = packages
            .GroupBy(p => p.Version, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (ambiguousPackages.Count > 0)
        {
            return Fail<ResolvedContents>(
                $"The following package versions match more than one package: {Quote(ambiguousPackages)}.");
        }

        var packageIdsByVersion = packages.ToDictionary(p => p.Version, p => p.Id, StringComparer.OrdinalIgnoreCase);

        var unresolvedPackages = packageVersions.Where(v => !packageIdsByVersion.ContainsKey(v)).ToList();
        if (unresolvedPackages.Count > 0)
            return Fail<ResolvedContents>($"Could not resolve the following release packages: {Quote(unresolvedPackages)}.");

        // Versions carried directly, resolved by product and number since a number alone is not unique.
        var versionRows = rows.Where(c => c.Kind == ReleaseContentKind.Version).ToList();

        var versionProductNames = versionRows
            .Select(c => Normalize(c.ProductName!))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var versionProducts = versionProductNames.Count == 0
            ? []
            : await _productManagementDbContext.Products
                .Where(p => versionProductNames.Contains(p.Name))
                .Select(p => new { p.Id, p.Name })
                .ToListAsync(cancellationToken);

        var ambiguousVersionProducts = versionProducts
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (ambiguousVersionProducts.Count > 0)
        {
            return Fail<ResolvedContents>(
                $"The following product names match more than one product: {Quote(ambiguousVersionProducts)}.");
        }

        var versionProductIds = versionProducts.ToDictionary(p => p.Name, p => p.Id, StringComparer.OrdinalIgnoreCase);

        var unresolvedVersionProducts = versionProductNames.Where(n => !versionProductIds.ContainsKey(n)).ToList();
        if (unresolvedVersionProducts.Count > 0)
            return Fail<ResolvedContents>($"Could not resolve the following products: {Quote(unresolvedVersionProducts)}.");

        // Every version reachable from this batch: the ones carried directly, plus the ones the chosen
        // packages carry — the latter are what the double-count rule is judged against.
        var packageComponentVersionIds = packages
            .SelectMany(p => p.Components)
            .Where(c => c.VersionId is not null)
            .Select(c => c.VersionId!.Value)
            .ToHashSet();

        var directProductIds = versionProductIds.Values.ToList();

        var candidateVersions = await _productManagementDbContext.Versions
            .Where(v => directProductIds.Contains(v.ProductId) || packageComponentVersionIds.Contains(v.Id))
            .Select(v => new { v.Id, v.ProductId, v.Number, v.ReleasedDate })
            .ToListAsync(cancellationToken);

        var namesByProductId = versionProductIds.ToDictionary(pair => pair.Value, pair => pair.Key);

        var versionIdsByKey = candidateVersions
            .Where(v => namesByProductId.ContainsKey(v.ProductId))
            .GroupBy(v => VersionKey(namesByProductId[v.ProductId], v.Number), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        var unresolvedVersions = versionRows
            .Select(c => VersionKey(c.ProductName!, c.VersionNumber!))
            .Where(k => !versionIdsByKey.ContainsKey(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (unresolvedVersions.Count > 0)
            return Fail<ResolvedContents>($"Could not resolve the following versions: {Quote(unresolvedVersions)}.");

        return Result.Success(new ResolvedContents(
            byRelease,
            packageIdsByVersion,
            versionIdsByKey,
            packages.ToDictionary(
                p => p.Id,
                p => (IReadOnlyList<Guid>)[.. p.Components.Where(c => c.VersionId is not null).Select(c => c.VersionId!.Value)]),
            [.. candidateVersions.Where(v => v.ReleasedDate is not null).Select(v => v.Id)],
            [.. packages.Where(p => p.ReleasedDate is not null).Select(p => p.Id)],
            candidateVersions.ToDictionary(
                v => v.Id,
                v => namesByProductId.TryGetValue(v.ProductId, out var name) ? $"{name} {v.Number}" : v.Number),
            packages.ToDictionary(p => p.Id, p => p.Version)));
    }

    /// <summary>
    /// Resolves the two statuses an imported release can hold, once for the whole batch.
    /// </summary>
    private async Task<Result<(StatusRef Initial, StatusRef Released)>> ResolveStatuses(
        CancellationToken cancellationToken)
    {
        // Product Management assigns workflows organization-wide, so the scope is null.
        var initial = await _statusResolver.Initial(
            ProductWorkflowOwners.Release.Key, scopeId: null, cancellationToken);
        if (initial.IsFailure)
        {
            _logger.LogError("Unable to resolve the initial release status. Error message: {Error}", initial.Error);
            return Result.Failure<(StatusRef, StatusRef)>(initial.Error);
        }

        var released = await _statusResolver.ForAlias(
            ProductWorkflowOwners.Release.Key, scopeId: null, (int)ProductStatusAlias.Released, cancellationToken);
        if (released.IsFailure)
        {
            _logger.LogError("Unable to resolve the released release status. Error message: {Error}", released.Error);
            return Result.Failure<(StatusRef, StatusRef)>(released.Error);
        }

        return Result.Success((initial.Value, released.Value));
    }

    /// <summary>Everything the batch's content rows resolve to, gathered once.</summary>
    private sealed record ResolvedContents(
        Dictionary<string, IReadOnlyList<ImportReleaseContentDto>> ByRelease,
        Dictionary<string, Guid> PackageIdsByVersion,
        Dictionary<string, Guid> VersionIdsByKey,
        Dictionary<Guid, IReadOnlyList<Guid>> VersionIdsByPackageId,
        HashSet<Guid> ReleasedVersionIds,
        HashSet<Guid> ReleasedPackageIds,
        Dictionary<Guid, string> VersionLabelsById,
        Dictionary<Guid, string> PackageLabelsById);

    /// <summary>Identifies a version: a number is only unique within its product.</summary>
    private static string VersionKey(string productName, string number) =>
        $"{Normalize(productName)} {Normalize(number)}";

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
