using System.Globalization;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NodaTime;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Interfaces.Organization;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.ProductManagement.Application.Products.Dtos;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Products.Imports;

/// <summary>
/// Imports a product catalog, parents before children so a child's parent always exists by the time it is
/// created.
/// </summary>
/// <remarks>
/// Per group, keyed on the root of each row's tree. Rows are not independent: a child names a parent row in
/// the same file, and a child whose parent was rejected has nowhere to hang. A parent must be a row in the
/// file, so every tree is closed within it, and grouping by the root keeps each tree in one chunk — the
/// runner never splits a group — where the pass can find every parent it created. One rejected product keeps
/// out its whole tree, siblings included; the runner has no unit smaller than the group to keep out.
/// </remarks>
public sealed class ProductImportDefinition(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<ProductImportDefinition> logger,
    IImportPayloadSerializer serializer,
    IDispatcher dispatcher) : ImportDefinition<ImportProductDto>(serializer)
{
    public const string ImportKey = "product-management.products";

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<ProductImportDefinition> _logger = logger;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ProductTeamCopies _teams = new(productManagementDbContext, dispatcher, dateTimeProvider, logger);

    public override string Key => ImportKey;

    /// <inheritdoc />
    public override async Task<IReadOnlyDictionary<Guid, ImportRecordLink>> DescribeCreatedRecords(
        IReadOnlyCollection<Guid> createdEntityIds,
        CancellationToken cancellationToken) =>
        await _productManagementDbContext.Products
            .AsNoTracking()
            .Where(x => createdEntityIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Key, Name = x.Name })
            .ToDictionaryAsync(
                x => x.Id,
                x => new ImportRecordLink(ImportRecordType.Product, x.Key.ToString(CultureInfo.InvariantCulture), x.Name),
                cancellationToken);
    public override string DisplayName => "Products";
    public override string PermissionAction => ApplicationAction.Import;
    public override string PermissionResource => ApplicationResource.Products;

    public override ImportAtomicity Atomicity => ImportAtomicity.PerGroup;
    public override string? GroupNoun => "product tree";

    // Saving chunk by chunk makes a larger file possible, but a run at that size is not yet proven end to
    // end. One tree is still one transaction whatever its size.
    public override int MaxRows => 10_000;

    /// <summary>
    /// The import id of the root each row's parent chain ends at, which only the whole file can say.
    /// </summary>
    /// <remarks>
    /// The submission command refuses a parent that is not in the file and a chain that loops, so every walk
    /// ends at a row with no parent. The walk still stops on a row it has seen, so a cycle cannot hang it.
    /// </remarks>
    protected override IReadOnlyList<string?> GroupKeys(IReadOnlyList<(string ImportId, ImportProductDto Row)> rows)
    {
        var parents = rows.ToDictionary(
            r => r.ImportId,
            r => r.Row.ParentImportId?.Trim(),
            StringComparer.OrdinalIgnoreCase);

        // Keyed by the id as the file wrote it on the row, not as a child spelled it, so every member of a
        // tree returns the same string.
        var canonical = rows.ToDictionary(r => r.ImportId, r => r.ImportId, StringComparer.OrdinalIgnoreCase);

        return [.. rows.Select(r =>
        {
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
            var current = r.ImportId;

            while (seen.Add(current) && parents[current] is { } parent && parents.ContainsKey(parent))
                current = parent;

            return canonical[current];
        })];
    }

    protected override IReadOnlyList<ImportPass<ImportProductDto>> Steps =>
    [
        new("CreateProducts", ImportPassScope.Chunked, CreateProducts),
    ];

    private async Task<Result> CreateProducts(ImportPassContext<ImportProductDto> context, CancellationToken cancellationToken)
    {
        var timestamp = _dateTimeProvider.Now;

        // One import run is one actor: the events say "the import", not "this person edited every row by
        // hand", while still recording who set it running.
        var actor = EventActor.Import(_currentUser.GetUserId());

        var typesByName = await ResolveProductTypes(context, cancellationToken);

        var statuses = await ResolveStatuses(cancellationToken);
        if (statuses.IsFailure)
            return Result.Failure(statuses.Error);
        var (initialStatus, statusesByName) = statuses.Value;

        var tagsByReference = await ResolveTags(context, cancellationToken);

        // Before any product is staged: building a missing team copy saves, and that save must carry nothing
        // but the copy, or a product tree the runner later keeps out would already be in the database.
        var teamsByEntry = await ResolveTeams(context, cancellationToken);

        var rowsByImportId = context.Rows.ToDictionary(r => r.ImportId, StringComparer.OrdinalIgnoreCase);

        // Populated as rows are created so a child can look its parent up. This is the whole reason the
        // rows are ordered parents-first.
        Dictionary<string, Product> createdByImportId = new(StringComparer.OrdinalIgnoreCase);

        var (ordered, cycled) = OrderParentsFirst(context.Rows, rowsByImportId);

        foreach (var row in ordered)
        {
            if (cycled.Contains(row.ImportId))
            {
                row.Failed("This product is part of a circular parent reference within the file.");
                continue;
            }

            var data = row.Data;

            if (!typesByName.TryGetValue(Normalize(data.ProductTypeName), out var productType))
            {
                row.Failed($"No active product type was found named '{data.ProductTypeName}'.");
                continue;
            }

            Guid? parentId = null;
            if (data.ParentImportId is { } parentImportId)
            {
                if (!createdByImportId.TryGetValue(parentImportId.Trim(), out var parent))
                {
                    row.Failed(
                        $"No product in this file has import id '{parentImportId}', named as this row's parent. "
                        + "A parent must be another row in the same file; a product already in the catalog cannot be named as one here.");
                    continue;
                }

                parentId = parent.Id;
            }

            var status = initialStatus;
            if (!string.IsNullOrWhiteSpace(data.Status) && !statusesByName.TryGetValue(Normalize(data.Status), out status))
            {
                row.Failed($"No status named '{data.Status}' belongs to the product workflow.");
                continue;
            }

            var product = Product.Create(
                Normalize(data.Name),
                data.Description,
                productType.Id,
                parentId,
                data.ExternalId,
                status,
                actor,
                timestamp);

            var tagged = ApplyTags(product, data, tagsByReference, actor, timestamp);
            if (tagged.IsFailure)
            {
                row.Failed(tagged.Error);
                continue;
            }

            var linked = LinkTeams(product, data, teamsByEntry, actor, timestamp);
            if (linked.IsFailure)
            {
                row.Failed(linked.Error);
                continue;
            }

            await _productManagementDbContext.Products.AddAsync(product, cancellationToken);

            createdByImportId.Add(row.ImportId, product);
            row.Created(product.Id);
        }

        return Result.Success();
    }

    /// <summary>
    /// Sorts the rows so every one follows its parent.
    /// </summary>
    /// <remarks>
    /// A file lists rows in whatever order someone wrote them, so a child may well appear above its
    /// parent. Creating in file order would leave that child pointing at a parent that does not exist yet,
    /// which the domain cannot catch — it is handed a parent id and never queries the tree.
    /// <para>
    /// A row caught in a cycle is rejected here rather than failing the pass, so the run names the rows
    /// involved. The submission command rejects a cycle before the run starts.
    /// </para>
    /// </remarks>
    private static (List<ImportRowItem<ImportProductDto>> Ordered, HashSet<string> Cycled) OrderParentsFirst(
        IReadOnlyList<ImportRowItem<ImportProductDto>> rows,
        Dictionary<string, ImportRowItem<ImportProductDto>> rowsByImportId)
    {
        List<ImportRowItem<ImportProductDto>> ordered = new(rows.Count);
        HashSet<string> placed = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> cycled = new(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            // Walks up to the root collecting anything not yet placed, then adds that chain
            // outermost-first. A row already placed by an earlier walk ends this one.
            List<ImportRowItem<ImportProductDto>> chain = [];
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

            var current = row;
            var cycle = false;

            while (true)
            {
                if (placed.Contains(current.ImportId))
                    break;

                if (!seen.Add(current.ImportId))
                {
                    cycle = true;
                    break;
                }

                chain.Add(current);

                if (current.Data.ParentImportId is not { } parentImportId)
                    break;

                if (!rowsByImportId.TryGetValue(parentImportId.Trim(), out var parent))
                    break;

                current = parent;
            }

            if (cycle)
            {
                foreach (var member in chain)
                    cycled.Add(member.ImportId);
            }

            for (var i = chain.Count - 1; i >= 0; i--)
            {
                ordered.Add(chain[i]);
                placed.Add(chain[i].ImportId);
            }
        }

        return (ordered, cycled);
    }

    private static Result ApplyTags(
        Product product,
        ImportProductDto data,
        Dictionary<string, (ProductTag Tag, ProductTagCategory Category)> tagsByReference,
        EventActor actor,
        Instant timestamp)
    {
        foreach (var reference in data.Tags)
        {
            if (!tagsByReference.TryGetValue(TagKey(reference), out var resolved))
                return Result.Failure($"No active tag '{reference.CategoryName}|{reference.TagName}' was found.");

            var tagged = product.Tag(resolved.Tag, resolved.Category, actor, timestamp);
            if (tagged.IsFailure)
                return Result.Failure($"Could not tag this product with '{reference.CategoryName}|{reference.TagName}': {tagged.Error}");
        }

        return Result.Success();
    }

    /// <summary>
    /// A team a file names: as Organization holds it, which decides whether it exists and is active, and the
    /// module's copy, which is what a product links to.
    /// </summary>
    private sealed record ResolvedTeam(ISimpleTeam Source, ProductTeam Copy);

    /// <summary>
    /// Links the row's teams to its own product, refusing the row when any one of them is unknown or inactive.
    /// </summary>
    private static Result LinkTeams(
        Product product,
        ImportProductDto data,
        Dictionary<string, ResolvedTeam> teamsByEntry,
        EventActor actor,
        Instant timestamp)
    {
        foreach (var entry in data.Teams ?? [])
        {
            if (!teamsByEntry.TryGetValue(entry.Trim(), out var team))
                return Result.Failure($"No team was found with the code or ID '{entry.Trim()}'.");

            if (!team.Source.IsActive)
                return Result.Failure($"The team '{team.Source.Name}' ({team.Source.Code.Value}) is inactive, and only an active team can be linked to a product.");

            var result = product.LinkTeam(team.Copy, actor, timestamp);
            if (result.IsFailure)
                return Result.Failure($"Could not link the team '{team.Source.Name}' ({team.Source.Code.Value}) to this product: {result.Error}");
        }

        return Result.Success();
    }

    /// <summary>
    /// Resolves every team the file names, keyed by the entry as written (case-insensitive). An entry absent
    /// from the answer names no team.
    /// </summary>
    /// <remarks>
    /// Organization answers whether each team exists, is active, and what it is called, in one query: it owns
    /// teams, and the module's copy can lag a rename or a deactivation. The copies are then read in one query,
    /// and any team without one yet is copied through <see cref="ProductTeamCopies"/>, so it links like any
    /// other. Inactive teams are kept, so the row can be told why it cannot link one.
    /// </remarks>
    private async Task<Dictionary<string, ResolvedTeam>> ResolveTeams(
        ImportPassContext<ImportProductDto> context, CancellationToken cancellationToken)
    {
        var entries = context.Rows
            .SelectMany(r => r.Data.Teams ?? [])
            .Select(e => e.Trim())
            .Where(e => e.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Dictionary<string, ResolvedTeam> resolved = new(StringComparer.OrdinalIgnoreCase);

        if (entries.Count == 0)
            return resolved;

        var ids = entries.Select(ProductTeamEntry.AsId).OfType<Guid>().ToList();
        var codes = entries.Select(ProductTeamEntry.AsCode).OfType<string>().ToList();

        var sources = await _dispatcher.Send(new GetTeamsByIdOrCodeQuery(ids, codes), cancellationToken);
        if (sources.Count == 0)
            return resolved;

        var sourceIds = sources.Select(t => t.Id).ToList();
        var copies = await _productManagementDbContext.ProductTeams
            .Where(t => sourceIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        foreach (var source in sources.Where(t => !copies.ContainsKey(t.Id)))
        {
            if (await _teams.Find(source.Id, cancellationToken) is { } copy)
                copies[source.Id] = copy;
        }

        var sourcesById = sources.ToDictionary(t => t.Id);
        var sourcesByCode = sources.ToDictionary(t => t.Code.Value, StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            var source = ProductTeamEntry.AsId(entry) is { } id
                ? sourcesById.GetValueOrDefault(id)
                : ProductTeamEntry.AsCode(entry) is { } code ? sourcesByCode.GetValueOrDefault(code) : null;

            if (source is not null && copies.TryGetValue(source.Id, out var copy))
                resolved.Add(entry, new ResolvedTeam(source, copy));
        }

        return resolved;
    }

    /// <summary>
    /// Resolves the active product types the file names. Type names carry a unique index, so an absent
    /// entry means the type does not exist or is inactive — neither of which can be assigned.
    /// </summary>
    private async Task<Dictionary<string, ProductType>> ResolveProductTypes(
        ImportPassContext<ImportProductDto> context, CancellationToken cancellationToken)
    {
        var typeNames = context.Rows
            .Select(r => Normalize(r.Data.ProductTypeName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return (await _productManagementDbContext.ProductTypes
                .Where(t => typeNames.Contains(t.Name) && t.IsActive)
                .ToListAsync(cancellationToken))
            .ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Loads the product workflow once, returning the initial status for rows naming none and a lookup for
    /// those that do.
    /// </summary>
    /// <remarks>
    /// Resolved through the governing workflow rather than by loading statuses directly, for the same
    /// reason <c>ChangeProductStatusCommand</c> does: that is what stops a row naming a status belonging
    /// to some other workflow.
    /// </remarks>
    private async Task<Result<(StatusRef Initial, Dictionary<string, StatusRef> ByName)>> ResolveStatuses(
        CancellationToken cancellationToken)
    {
        // Product Management assigns workflows organization-wide, so the scope is null.
        var workflow = await _statusResolver.ForScope(
            ProductWorkflowOwners.Product.Key, scopeId: null, cancellationToken);

        if (workflow.IsFailure)
        {
            _logger.LogError("Unable to resolve the product workflow. Error message: {Error}", workflow.Error);
            return Result.Failure<(StatusRef, Dictionary<string, StatusRef>)>(workflow.Error);
        }

        var initial = await _statusResolver.Initial(
            ProductWorkflowOwners.Product.Key, scopeId: null, cancellationToken);

        if (initial.IsFailure)
        {
            _logger.LogError("Unable to resolve the initial product status. Error message: {Error}", initial.Error);
            return Result.Failure<(StatusRef, Dictionary<string, StatusRef>)>(initial.Error);
        }

        var statusesByName = workflow.Value.Statuses.ToDictionary(
            s => s.Name, StatusRef.From, StringComparer.OrdinalIgnoreCase);

        return Result.Success((initial.Value, statusesByName));
    }

    /// <summary>
    /// Resolves every active <c>Category|Tag</c> pair the file names, keyed by both halves.
    /// </summary>
    /// <remarks>
    /// Both halves are needed because a tag name is unique only within its axis — two axes may each hold a
    /// <c>gold</c>, and resolving on the tag alone would pick whichever came back first.
    /// <para>
    /// Categories are loaded with their tags rather than querying tags directly: the aggregate needs the
    /// category anyway, since <see cref="ProductTagCategory.AllowsMany"/> decides whether a second tag on
    /// one axis joins the first or replaces it.
    /// </para>
    /// </remarks>
    private async Task<Dictionary<string, (ProductTag Tag, ProductTagCategory Category)>> ResolveTags(
        ImportPassContext<ImportProductDto> context, CancellationToken cancellationToken)
    {
        var references = context.Rows
            .SelectMany(r => r.Data.Tags)
            .DistinctBy(TagKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Dictionary<string, (ProductTag, ProductTagCategory)> resolved = new(StringComparer.OrdinalIgnoreCase);

        if (references.Count == 0)
            return resolved;

        var categoryNames = references
            .Select(r => Normalize(r.CategoryName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var categoriesByName = (await _productManagementDbContext.ProductTagCategories
                .Include(c => c.Tags)
                .Where(c => categoryNames.Contains(c.Name) && c.IsActive)
                .ToListAsync(cancellationToken))
            .ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);

        foreach (var reference in references)
        {
            if (!categoriesByName.TryGetValue(Normalize(reference.CategoryName), out var category))
                continue;

            var tag = category.Tags.FirstOrDefault(
                t => t.IsActive && string.Equals(t.Name, Normalize(reference.TagName), StringComparison.OrdinalIgnoreCase));

            if (tag is not null)
                resolved.Add(TagKey(reference), (tag, category));
        }

        return resolved;
    }

    /// <summary>The lookup key for a tag reference: both halves, since neither is unique alone.</summary>
    private static string TagKey(ProductTagReference reference) =>
        $"{Normalize(reference.CategoryName)}|{Normalize(reference.TagName)}";

    private static string Normalize(string value) => value.Trim();
}
