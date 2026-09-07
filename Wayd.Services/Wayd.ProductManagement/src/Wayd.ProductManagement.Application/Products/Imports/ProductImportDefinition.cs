using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NodaTime;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Events;
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
/// <see cref="ImportPassScope.WholeSet"/> because rows are not independent: a child row names a parent row
/// in the same file, so a chunk could be handed a child whose parent has not been created.
/// <para>
/// Atomic, matching the single save the command it replaces did. A half-applied catalog is a tree with
/// missing branches and nothing to say which, and a child whose parent was rejected has nowhere to hang.
/// </para>
/// </remarks>
public sealed class ProductImportDefinition(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<ProductImportDefinition> logger,
    IImportPayloadSerializer serializer) : ImportDefinition<ImportProductDto>(serializer)
{
    public const string ImportKey = "product-management.products";

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<ProductImportDefinition> _logger = logger;

    public override string Key => ImportKey;
    public override string DisplayName => "Products";
    public override string PermissionAction => ApplicationAction.Import;
    public override string PermissionResource => ApplicationResource.Products;

    public override ImportAtomicity Atomicity => ImportAtomicity.Atomic;

    // An atomic import cannot be split, so the row cap is what actually bounds one run.
    public override int MaxRows => 10_000;

    protected override IReadOnlyList<ImportPass<ImportProductDto>> Steps =>
    [
        new("CreateProducts", ImportPassScope.WholeSet, CreateProducts),
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
