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
using Wayd.ProductManagement.Application.Versions.Dtos;
using Wayd.ProductManagement.Domain;

// The delivery artifact record, not System.Version.
using Version = Wayd.ProductManagement.Domain.Models.Version;

namespace Wayd.ProductManagement.Application.Versions.Imports;

/// <summary>
/// Imports versions, each planned against its product and then walked to the state its dates describe by
/// replaying the real lifecycle transitions.
/// </summary>
/// <remarks>
/// There is no status column. A version's status follows from what happened to it: a row with no dates is
/// planned, a cut date makes it ready, and a released date makes it released. Replaying the transitions
/// rather than assigning a status is what gives an imported version the same status history a hand-entered
/// one would have.
/// <para>
/// Unlike a PPM program, a version needs no finalize pass. Nothing about a released version has to exist
/// first — <c>Version.MarkReleased</c> has no precondition that the version was ever cut, which the domain
/// documents as the case historical import depends on.
/// </para>
/// <para>
/// Atomic, matching the single save the command it replaces did.
/// </para>
/// </remarks>
public sealed class VersionImportDefinition(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<VersionImportDefinition> logger,
    IImportPayloadSerializer serializer) : ImportDefinition<ImportVersionDto>(serializer)
{
    public const string ImportKey = "product-management.versions";

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<VersionImportDefinition> _logger = logger;

    public override string Key => ImportKey;
    public override string DisplayName => "Versions";
    public override string PermissionAction => ApplicationAction.Import;
    public override string PermissionResource => ApplicationResource.Delivery;

    public override ImportAtomicity Atomicity => ImportAtomicity.Atomic;

    // An atomic import cannot be split, so the row cap is what actually bounds one run.
    public override int MaxRows => 10_000;

    protected override IReadOnlyList<ImportPass<ImportVersionDto>> Steps =>
    [
        new("CreateVersions", ImportPassScope.WholeSet, CreateVersions),
    ];

    private async Task<Result> CreateVersions(ImportPassContext<ImportVersionDto> context, CancellationToken cancellationToken)
    {
        var timestamp = _dateTimeProvider.Now;

        // One import run is one actor: the events say "the import", not "this person recorded every
        // shipment by hand", while still recording who set it running.
        var actor = EventActor.Import(_currentUser.GetUserId());

        var productsById = await ResolveProducts(context, cancellationToken);
        var takenKeys = await ResolveTakenKeys(context, cancellationToken);

        var statuses = await ResolveStatuses(cancellationToken);
        if (statuses.IsFailure)
            return Result.Failure(statuses.Error);

        foreach (var row in context.Accepted)
        {
            var data = row.Data;

            if (!productsById.TryGetValue(data.ProductId, out var product))
            {
                row.Failed($"No product was found with id '{data.ProductId}'.");
                continue;
            }

            // Checked here as well as by the aggregate so the message names the product rather than
            // surfacing as a generic refusal from Create.
            if (!product.IsReleasable)
            {
                row.Failed($"Versions cannot be cut against product '{product.Name}', whose type is not releasable.");
                continue;
            }

            // Unlike products — where a repeated name is ordinary — a product holding two versions with
            // the same number is a mistake, so re-running a file is refused rather than silently
            // duplicating shipments.
            var key = VersionKey(data.ProductId, data.Number);
            if (takenKeys.Contains(key))
            {
                row.Failed($"Product '{product.Name}' already has a version numbered '{data.Number}'.");
                continue;
            }

            var created = Version.Create(
                product.Id,
                Normalize(data.Number),
                data.Name,
                data.TargetDate,
                data.Sequence,
                product.IsReleasable,
                statuses.Value.Initial,
                product.Name,
                actor,
                timestamp);
            if (created.IsFailure)
            {
                row.Failed($"Could not plan version '{data.Number}' for product '{product.Name}': {created.Error}");
                continue;
            }

            var walked = Walk(created.Value, data, statuses.Value, product.Name, actor, timestamp);
            if (walked.IsFailure)
            {
                row.Failed(walked.Error);
                continue;
            }

            await _productManagementDbContext.Versions.AddAsync(created.Value, cancellationToken);

            // Taken within the file as well as against the database: rows are applied before anything is
            // saved, so a repeat would otherwise reach the save undetected.
            takenKeys.Add(key);
            row.Created(created.Value.Id);
        }

        return Result.Success();
    }

    /// <summary>
    /// Walks a freshly planned version to the state its dates describe, in the order the events actually
    /// happen.
    /// </summary>
    /// <remarks>
    /// Each step is the same domain method a person's click would reach, so the status history reads as
    /// though the version were recorded as it went rather than in one sitting. Notes are applied last:
    /// <c>UpdateDetails</c> is an edit rather than a lifecycle step, and a withdrawn or released version
    /// still accepts one.
    /// </remarks>
    private static Result Walk(
        Version version,
        ImportVersionDto data,
        VersionStatuses statuses,
        string productName,
        EventActor actor,
        Instant timestamp)
    {
        if (data.CutDate is not null)
        {
            var cut = version.Cut(data.CutDate.Value, statuses.Ready, productName, actor, timestamp);
            if (cut.IsFailure)
                return Result.Failure($"Could not cut version '{data.Number}' for product '{productName}': {cut.Error}");
        }

        if (data.ReleasedDate is not null)
        {
            var released = version.MarkReleased(data.ReleasedDate.Value, statuses.Released, productName, actor, timestamp);
            if (released.IsFailure)
                return Result.Failure($"Could not release version '{data.Number}' for product '{productName}': {released.Error}");
        }

        if (!string.IsNullOrWhiteSpace(data.Notes))
        {
            var notes = version.UpdateDetails(
                Normalize(data.Number), data.Name, data.Notes, data.Sequence, actor, timestamp);
            if (notes.IsFailure)
                return Result.Failure($"Could not set notes on version '{data.Number}' for product '{productName}': {notes.Error}");
        }

        return Result.Success();
    }

    /// <summary>Resolves the products the file names, with the releasability their type decides.</summary>
    private async Task<Dictionary<Guid, ResolvedProduct>> ResolveProducts(
        ImportPassContext<ImportVersionDto> context, CancellationToken cancellationToken)
    {
        var productIds = context.Rows.Select(r => r.Data.ProductId).Distinct().ToList();

        return await _productManagementDbContext.Products
            .Where(p => productIds.Contains(p.Id))
            .Join(
                _productManagementDbContext.ProductTypes,
                p => p.ProductTypeId,
                t => t.Id,
                (p, t) => new ResolvedProduct(p.Id, p.Name, t.IsReleasable))
            .ToDictionaryAsync(p => p.Id, p => p, cancellationToken);
    }

    /// <summary>The (product, number) pairs the file names that already exist.</summary>
    private async Task<HashSet<string>> ResolveTakenKeys(
        ImportPassContext<ImportVersionDto> context, CancellationToken cancellationToken)
    {
        var productIds = context.Rows.Select(r => r.Data.ProductId).Distinct().ToList();

        return (await _productManagementDbContext.Versions
                .Where(v => productIds.Contains(v.ProductId))
                .Select(v => new { v.ProductId, v.Number })
                .ToListAsync(cancellationToken))
            .Select(v => VersionKey(v.ProductId, v.Number))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Resolves the three statuses a version can reach during an import, once for the whole run.</summary>
    private async Task<Result<VersionStatuses>> ResolveStatuses(CancellationToken cancellationToken)
    {
        // Product Management assigns workflows organization-wide, so the scope is null.
        var initial = await _statusResolver.Initial(ProductWorkflowOwners.Version.Key, scopeId: null, cancellationToken);
        if (initial.IsFailure)
        {
            _logger.LogError("Unable to resolve the initial version status. Error message: {Error}", initial.Error);
            return Result.Failure<VersionStatuses>(initial.Error);
        }

        var ready = await _statusResolver.ForAlias(
            ProductWorkflowOwners.Version.Key, scopeId: null, (int)ProductStatusAlias.Ready, cancellationToken);
        if (ready.IsFailure)
        {
            _logger.LogError("Unable to resolve the ready version status. Error message: {Error}", ready.Error);
            return Result.Failure<VersionStatuses>(ready.Error);
        }

        var released = await _statusResolver.ForAlias(
            ProductWorkflowOwners.Version.Key, scopeId: null, (int)ProductStatusAlias.Released, cancellationToken);
        if (released.IsFailure)
        {
            _logger.LogError("Unable to resolve the released version status. Error message: {Error}", released.Error);
            return Result.Failure<VersionStatuses>(released.Error);
        }

        return Result.Success(new VersionStatuses(initial.Value, ready.Value, released.Value));
    }

    /// <summary>A product the file names, with what its type permits.</summary>
    private sealed record ResolvedProduct(Guid Id, string Name, bool IsReleasable);

    /// <summary>The statuses an imported version can pass through.</summary>
    private sealed record VersionStatuses(StatusRef Initial, StatusRef Ready, StatusRef Released);

    /// <summary>Identifies a version: the number alone is only unique within its product.</summary>
    private static string VersionKey(Guid productId, string number) => $"{productId} {Normalize(number)}";

    private static string Normalize(string value) => value.Trim();
}
