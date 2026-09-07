using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.ProductManagement.Application.ReleasePackages.Dtos;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.ReleasePackages.Imports;

/// <summary>
/// Imports release packages, each assembled with its manifest and then marked released where the row says
/// it shipped.
/// </summary>
/// <remarks>
/// The manifest arrives on the row rather than afterwards, because a package cannot exist without one — an
/// empty manifest says nothing about what shipped, and the aggregate refuses it. That is why the import
/// takes two files: one row per package, and one row per manifest line naming the package row it belongs
/// to.
/// <para>
/// A manifest line names a version by string. Where a matching version record exists for that product the
/// line is linked to it; where none does the string stands alone, which is the carried-forward case the
/// domain models deliberately — a component that was already running and was never cut here. An unmatched
/// string is therefore <em>not</em> an error.
/// </para>
/// <para>
/// Atomic, matching the single save the command it replaces did.
/// </para>
/// </remarks>
public sealed class ReleasePackageImportDefinition(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<ReleasePackageImportDefinition> logger,
    IImportPayloadSerializer serializer) : ImportDefinition<ImportReleasePackageDto>(serializer)
{
    public const string ImportKey = "product-management.release-packages";

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<ReleasePackageImportDefinition> _logger = logger;

    public override string Key => ImportKey;
    public override string DisplayName => "Release Packages";
    public override string PermissionAction => ApplicationAction.Import;
    public override string PermissionResource => ApplicationResource.Delivery;

    public override ImportAtomicity Atomicity => ImportAtomicity.Atomic;

    // An atomic import cannot be split, so the row cap is what actually bounds one run.
    public override int MaxRows => 10_000;

    protected override IReadOnlyList<ImportPass<ImportReleasePackageDto>> Steps =>
    [
        new("CreatePackages", ImportPassScope.WholeSet, CreatePackages),
    ];

    private async Task<Result> CreatePackages(ImportPassContext<ImportReleasePackageDto> context, CancellationToken cancellationToken)
    {
        var timestamp = _dateTimeProvider.Now;

        // One import run is one actor: the events say "the import", not "this person assembled every
        // package by hand", while still recording who set it running.
        var actor = EventActor.Import(_currentUser.GetUserId());

        var takenVersions = await ResolveTakenVersions(context, cancellationToken);
        var productIds = await ResolveProductIds(context, cancellationToken);
        var versionIds = await ResolveComponentVersions(productIds, cancellationToken);

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
                row.Failed($"A release package already exists with version '{version}'.");
                continue;
            }

            var unresolvedProducts = data.Components
                .Where(c => !productIds.Contains(c.ProductId))
                .Select(c => c.ProductId.ToString())
                .Distinct()
                .ToList();
            if (unresolvedProducts.Count > 0)
            {
                row.Failed($"This manifest names a product that does not exist: {Quote(unresolvedProducts)}.");
                continue;
            }

            // A miss must stay null rather than becoming Guid.Empty: the nullable VersionId is what
            // records a carried-forward component whose version was never cut here.
            var components = data.Components
                .Select(c => (
                    c.ProductId,
                    VersionId: versionIds.TryGetValue(ComponentKey(c.ProductId, c.VersionNumber), out var versionId)
                        ? versionId
                        : (Guid?)null,
                    Version: Normalize(c.VersionNumber),
                    c.Kind))
                .ToList();

            var created = ReleasePackage.Create(
                version, data.Name, data.TargetDate, components, initialStatus, actor, timestamp);
            if (created.IsFailure)
            {
                row.Failed($"Could not assemble package '{version}': {created.Error}");
                continue;
            }

            var package = created.Value;

            if (data.ReleasedDate is not null)
            {
                var released = package.MarkReleased(data.ReleasedDate.Value, releasedStatus, actor, timestamp);
                if (released.IsFailure)
                {
                    row.Failed($"Could not release package '{version}': {released.Error}");
                    continue;
                }
            }

            await _productManagementDbContext.ReleasePackages.AddAsync(package, cancellationToken);

            // Taken within the file as well as against the database: rows are applied before anything is
            // saved, so a repeat would otherwise reach the save undetected.
            takenVersions.Add(version);
            row.Created(package.Id);
        }

        return Result.Success();
    }

    private async Task<HashSet<string>> ResolveTakenVersions(
        ImportPassContext<ImportReleasePackageDto> context, CancellationToken cancellationToken)
    {
        var versions = context.Rows.Select(r => Normalize(r.Data.Version)).ToList();

        return (await _productManagementDbContext.ReleasePackages
                .AsNoTracking()
                .Where(p => versions.Contains(p.Version))
                .Select(p => p.Version)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The products a manifest line names that exist.
    /// </summary>
    /// <remarks>
    /// Releasability is deliberately <em>not</em> checked: a manifest records what was in the box, and a
    /// carried-forward component may be a node no version was ever cut against.
    /// </remarks>
    private async Task<HashSet<Guid>> ResolveProductIds(
        ImportPassContext<ImportReleasePackageDto> context, CancellationToken cancellationToken)
    {
        var productIds = context.Rows
            .SelectMany(r => r.Data.Components)
            .Select(c => c.ProductId)
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
    /// Links manifest lines to version records where one matches, keyed by product and number.
    /// </summary>
    /// <remarks>
    /// A miss is not a failure. A carried-forward component frequently names a version that was never cut
    /// in Wayd, and recording the string without a link is exactly what lets a manifest answer "what was
    /// running" for every component rather than only the changed ones.
    /// </remarks>
    private async Task<Dictionary<string, Guid>> ResolveComponentVersions(
        HashSet<Guid> productIds, CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
            return new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        var ids = productIds.ToList();

        // A product may hold the same number only once, so the first match is the only match.
        return (await _productManagementDbContext.Versions
                .AsNoTracking()
                .Where(v => ids.Contains(v.ProductId))
                .Select(v => new { v.Id, v.ProductId, v.Number })
                .ToListAsync(cancellationToken))
            .GroupBy(v => ComponentKey(v.ProductId, v.Number), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Resolves the two statuses an imported package can hold, once for the whole run.</summary>
    private async Task<Result<(StatusRef Initial, StatusRef Released)>> ResolveStatuses(CancellationToken cancellationToken)
    {
        // Product Management assigns workflows organization-wide, so the scope is null.
        var initial = await _statusResolver.Initial(
            ProductWorkflowOwners.ReleasePackage.Key, scopeId: null, cancellationToken);
        if (initial.IsFailure)
        {
            _logger.LogError("Unable to resolve the initial release package status. Error message: {Error}", initial.Error);
            return Result.Failure<(StatusRef, StatusRef)>(initial.Error);
        }

        var released = await _statusResolver.ForAlias(
            ProductWorkflowOwners.ReleasePackage.Key, scopeId: null, (int)ProductStatusAlias.Released, cancellationToken);
        if (released.IsFailure)
        {
            _logger.LogError("Unable to resolve the released release package status. Error message: {Error}", released.Error);
            return Result.Failure<(StatusRef, StatusRef)>(released.Error);
        }

        return Result.Success((initial.Value, released.Value));
    }

    /// <summary>Identifies a component version: the number alone is only unique within its product.</summary>
    private static string ComponentKey(Guid productId, string versionNumber) =>
        $"{productId} {Normalize(versionNumber)}";

    private static string Normalize(string value) => value.Trim();

    private static string Quote(IEnumerable<string> values) => string.Join(", ", values.Select(v => $"'{v}'"));
}
