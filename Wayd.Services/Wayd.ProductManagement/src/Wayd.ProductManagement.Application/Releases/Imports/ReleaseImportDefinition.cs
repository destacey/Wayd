using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NodaTime;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.ProductManagement.Application.Releases.Dtos;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Releases.Imports;

/// <summary>
/// Imports releases, each planned, given its contents, and then announced where the row says it was.
/// </summary>
/// <remarks>
/// The three steps run in that order because the domain requires it: contents cannot be amended once a
/// release is announced, so a release that ships must receive them first. This is the one importer in the
/// module whose steps are genuinely ordered rather than merely convenient.
/// <para>
/// It is also the only one with a cross-record precondition. A release refuses to be announced while
/// anything it carries has not shipped — the one claim a release can make that its own contents
/// contradict. A row saying a release was announced while one of its versions has no released date is
/// therefore rejected rather than imported unannounced, because quietly demoting it would record
/// something the file did not say.
/// </para>
/// <para>
/// Atomic, matching the single save the command it replaces did.
/// </para>
/// </remarks>
public sealed class ReleaseImportDefinition(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<ReleaseImportDefinition> logger,
    IImportPayloadSerializer serializer) : ImportDefinition<ImportReleaseDto>(serializer)
{
    public const string ImportKey = "product-management.releases";

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<ReleaseImportDefinition> _logger = logger;

    public override string Key => ImportKey;
    public override string DisplayName => "Releases";
    public override string PermissionAction => ApplicationAction.Import;
    public override string PermissionResource => ApplicationResource.Releases;

    public override ImportAtomicity Atomicity => ImportAtomicity.Atomic;

    // An atomic import cannot be split, so the row cap is what actually bounds one run.
    public override int MaxRows => 10_000;

    protected override IReadOnlyList<ImportPass<ImportReleaseDto>> Steps =>
    [
        new("CreateReleases", ImportPassScope.WholeSet, CreateReleases),
    ];

    private async Task<Result> CreateReleases(ImportPassContext<ImportReleaseDto> context, CancellationToken cancellationToken)
    {
        var timestamp = _dateTimeProvider.Now;

        // One import run is one actor: the events say "the import", not "this person announced every
        // release by hand", while still recording who set it running.
        var actor = EventActor.Import(_currentUser.GetUserId());

        var takenVersions = await ResolveTakenVersions(context, cancellationToken);
        var productIds = await ResolveProductIds(context, cancellationToken);
        var contents = await ResolveContents(context, cancellationToken);

        var statuses = await ResolveStatuses(cancellationToken);
        if (statuses.IsFailure)
            return Result.Failure(statuses.Error);
        var (initialStatus, releasedStatus) = statuses.Value;

        foreach (var row in context.Accepted)
        {
            var data = row.Data;
            var version = Normalize(data.Version);

            if (takenVersions.Contains(version))
            {
                row.Failed($"A release already exists with version '{version}'.");
                continue;
            }

            if (data.ProductId is { } productId && !productIds.Contains(productId))
            {
                row.Failed($"No product was found with id '{productId}'.");
                continue;
            }

            var unresolved = UnresolvedContents(data, contents);
            if (unresolved.Count > 0)
            {
                row.Failed($"This release announces content that does not exist: {Quote(unresolved)}.");
                continue;
            }

            var created = Release.Create(
                data.ProductId,
                version,
                data.Name,
                data.TargetDate,
                data.Sequence,
                initialStatus,
                actor,
                timestamp);
            if (created.IsFailure)
            {
                row.Failed($"Could not plan release '{version}': {created.Error}");
                continue;
            }

            var release = created.Value;

            var applied = ApplyContents(release, data, contents, actor, timestamp);
            if (applied.IsFailure)
            {
                row.Failed(applied.Error);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(data.Notes))
            {
                var notes = release.UpdateDetails(
                    version, data.Name, data.Notes, data.ProductId, data.Sequence, actor, timestamp);
                if (notes.IsFailure)
                {
                    row.Failed($"Could not set notes on release '{version}': {notes.Error}");
                    continue;
                }
            }

            if (data.ReleasedDate is not null)
            {
                var announced = Announce(release, data, contents, releasedStatus, actor, timestamp);
                if (announced.IsFailure)
                {
                    row.Failed(announced.Error);
                    continue;
                }
            }

            await _productManagementDbContext.Releases.AddAsync(release, cancellationToken);

            // Taken within the file as well as against the database: rows are applied before anything is
            // saved, so a repeat would otherwise reach the save undetected.
            takenVersions.Add(version);
            row.Created(release.Id);
        }

        return Result.Success();
    }

    /// <summary>
    /// Sets what a release announces, before it can be announced.
    /// </summary>
    /// <remarks>
    /// Both routes go in one call because the invariant spans them: a version shipping inside one of the
    /// release's packages must not also be carried directly, or one shipment would be announced twice. The
    /// aggregate cannot load a package's manifest, so the versions reachable through the chosen packages
    /// are resolved here and handed in.
    /// </remarks>
    private static Result ApplyContents(
        Release release,
        ImportReleaseDto data,
        ResolvedContents contents,
        EventActor actor,
        Instant timestamp)
    {
        if (data.Contents.Count == 0)
            return Result.Success();

        var packageIds = data.Contents
            .Where(c => c.Kind == ReleaseContentKind.Package)
            .Select(c => c.PackageId!.Value)
            .ToList();

        var versionIds = data.Contents
            .Where(c => c.Kind == ReleaseContentKind.Version)
            .Select(c => c.VersionId!.Value)
            .ToList();

        var versionIdsInPackages = packageIds
            .SelectMany(id => contents.VersionIdsByPackageId.GetValueOrDefault(id, []))
            .Distinct()
            .ToList();

        var result = release.SetContents(versionIds, packageIds, versionIdsInPackages, actor, timestamp);

        return result.IsFailure
            ? Result.Failure($"Could not set contents on release '{data.Version}': {result.Error}")
            : Result.Success();
    }

    /// <summary>
    /// Announces a release, refusing where anything it carries has not shipped.
    /// </summary>
    /// <remarks>
    /// The aggregate holds ids rather than records, so whether its contents have shipped is resolved here.
    /// The refusal names what is holding the release back: without that, the message says only that
    /// something is unshipped and leaves the reader to find it.
    /// </remarks>
    private static Result Announce(
        Release release,
        ImportReleaseDto data,
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
            return Result.Failure(
                $"Release '{data.Version}' is marked as released but carries content that has not shipped: {Quote(unshipped)}. "
                + "Import those with a released date first, or leave this release's ReleasedDate empty.");
        }

        var result = release.MarkReleased(
            data.ReleasedDate!.Value, hasUnreleasedContents: false, releasedStatus, actor, timestamp);

        return result.IsFailure
            ? Result.Failure($"Could not release '{data.Version}': {result.Error}")
            : Result.Success();
    }

    /// <summary>
    /// The labels of anything a row announces that does not exist.
    /// </summary>
    /// <remarks>
    /// Unlike a package manifest — where an unmatched version is the carried-forward case and perfectly
    /// legitimate — a release's contents must resolve. A release announcing something that is not in Wayd
    /// is a mistyped reference, not a record of history.
    /// </remarks>
    private static List<string> UnresolvedContents(ImportReleaseDto data, ResolvedContents contents) =>
    [
        .. data.Contents
            .Where(c => c.Kind == ReleaseContentKind.Package && !contents.PackageLabelsById.ContainsKey(c.PackageId!.Value))
            .Select(c => c.PackageId!.Value.ToString()),
        .. data.Contents
            .Where(c => c.Kind == ReleaseContentKind.Version && !contents.VersionLabelsById.ContainsKey(c.VersionId!.Value))
            .Select(c => c.VersionId!.Value.ToString()),
    ];

    private async Task<HashSet<string>> ResolveTakenVersions(
        ImportPassContext<ImportReleaseDto> context, CancellationToken cancellationToken)
    {
        var versions = context.Rows.Select(r => Normalize(r.Data.Version)).ToList();

        return (await _productManagementDbContext.Releases
                .AsNoTracking()
                .Where(r => versions.Contains(r.Version))
                .Select(r => r.Version)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<HashSet<Guid>> ResolveProductIds(
        ImportPassContext<ImportReleaseDto> context, CancellationToken cancellationToken)
    {
        var productIds = context.Rows
            .Select(r => r.Data.ProductId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (productIds.Count == 0)
            return [];

        return (await _productManagementDbContext.Products
                .AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();
    }

    /// <summary>
    /// Resolves every package and version the file announces, along with what has shipped and what each
    /// package carries.
    /// </summary>
    private async Task<ResolvedContents> ResolveContents(
        ImportPassContext<ImportReleaseDto> context, CancellationToken cancellationToken)
    {
        var rows = context.Rows.SelectMany(r => r.Data.Contents).ToList();

        var packageIds = rows
            .Where(c => c.Kind == ReleaseContentKind.Package && c.PackageId.HasValue)
            .Select(c => c.PackageId!.Value)
            .Distinct()
            .ToList();

        var packages = packageIds.Count == 0
            ? []
            : await _productManagementDbContext.ReleasePackages
                .AsNoTracking()
                .Include(p => p.Components)
                .Where(p => packageIds.Contains(p.Id))
                .ToListAsync(cancellationToken);

        // Every version reachable from this file: the ones carried directly, plus the ones the chosen
        // packages carry — the latter are what the double-count rule is judged against.
        var packageComponentVersionIds = packages
            .SelectMany(p => p.Components)
            .Where(c => c.VersionId is not null)
            .Select(c => c.VersionId!.Value)
            .ToHashSet();

        var directVersionIds = rows
            .Where(c => c.Kind == ReleaseContentKind.Version && c.VersionId.HasValue)
            .Select(c => c.VersionId!.Value)
            .ToHashSet();

        var wantedVersionIds = directVersionIds.Concat(packageComponentVersionIds).Distinct().ToList();

        var versions = wantedVersionIds.Count == 0
            ? []
            : await _productManagementDbContext.Versions
                .AsNoTracking()
                .Where(v => wantedVersionIds.Contains(v.Id))
                .Select(v => new { v.Id, v.Number, v.ReleasedDate })
                .ToListAsync(cancellationToken);

        return new ResolvedContents(
            packages.ToDictionary(
                p => p.Id,
                p => (IReadOnlyList<Guid>)[.. p.Components.Where(c => c.VersionId is not null).Select(c => c.VersionId!.Value)]),
            [.. versions.Where(v => v.ReleasedDate is not null).Select(v => v.Id)],
            [.. packages.Where(p => p.ReleasedDate is not null).Select(p => p.Id)],
            versions.ToDictionary(v => v.Id, v => v.Number),
            packages.ToDictionary(p => p.Id, p => p.Version));
    }

    /// <summary>Resolves the two statuses an imported release can hold, once for the whole run.</summary>
    private async Task<Result<(StatusRef Initial, StatusRef Released)>> ResolveStatuses(CancellationToken cancellationToken)
    {
        // Product Management assigns workflows organization-wide, so the scope is null.
        var initial = await _statusResolver.Initial(ProductWorkflowOwners.Release.Key, scopeId: null, cancellationToken);
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

    /// <summary>What the file's releases point at, and what of it has already shipped.</summary>
    private sealed record ResolvedContents(
        Dictionary<Guid, IReadOnlyList<Guid>> VersionIdsByPackageId,
        HashSet<Guid> ReleasedVersionIds,
        HashSet<Guid> ReleasedPackageIds,
        Dictionary<Guid, string> VersionLabelsById,
        Dictionary<Guid, string> PackageLabelsById);

    private static string Normalize(string value) => value.Trim();

    private static string Quote(IEnumerable<string> values) => string.Join(", ", values.Select(v => $"'{v}'"));
}
