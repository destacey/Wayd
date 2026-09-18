using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Events;
using Wayd.ProductManagement.Application.Products.Dtos;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Products.Imports;

/// <summary>
/// Imports product dependencies, product by product.
/// </summary>
/// <remarks>
/// Per group, keyed on the product that holds the links: a product's dependencies are one aggregate, and
/// half of them misstates what it relies on as surely as none — a strength change whose ended row applied
/// without its replacement leaves a gap in the history. Other products' links are unaffected by one product's
/// bad row, which an all-or-nothing file of a whole estate's relationships would not survive.
/// <para>
/// A product's rows are applied in date order, whatever order the file lists them in, because the domain's
/// overlap rule judges each link against the ones recorded before it. The first rejection stops the product:
/// the runner keeps its other rows out anyway, and carrying on would only add errors that follow from the
/// first.
/// </para>
/// </remarks>
public sealed class ProductDependencyImportDefinition(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    IImportPayloadSerializer serializer) : ImportDefinition<ImportProductDependencyDto>(serializer)
{
    public const string ImportKey = "product-management.product-dependencies";

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public override string Key => ImportKey;
    public override string DisplayName => "Product Dependencies";
    public override string PermissionAction => ApplicationAction.Import;
    public override string PermissionResource => ApplicationResource.Products;

    public override ImportAtomicity Atomicity => ImportAtomicity.PerGroup;
    public override string? GroupNoun => "product";

    // Formatted the one way a Guid formats by default, so two rows naming a product in different casing still
    // land in the same group.
    protected override string? GroupKey(ImportProductDependencyDto row) => row.ProductId.ToString();

    protected override IReadOnlyList<ImportPass<ImportProductDependencyDto>> Steps =>
    [
        new("AddDependencies", ImportPassScope.Chunked, AddDependencies),
    ];

    private async Task<Result> AddDependencies(
        ImportPassContext<ImportProductDependencyDto> context, CancellationToken cancellationToken)
    {
        var timestamp = _dateTimeProvider.Now;
        var today = _dateTimeProvider.Today;
        var actor = EventActor.Import(_currentUser.GetUserId());

        var productIds = context.Rows.Select(r => r.Data.ProductId).ToHashSet();
        var products = await _productManagementDbContext.Products
            .Include(p => p.Dependencies)
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        // The whole tree, once per chunk rather than two ancestry walks per row: the composition check is
        // only as good as the ancestries it is given, and a chunk touches products all over the catalog.
        var parents = await _productManagementDbContext.Products
            .Select(p => new { p.Id, p.ParentId })
            .ToDictionaryAsync(p => p.Id, p => p.ParentId, cancellationToken);

        foreach (var group in context.Rows.GroupBy(r => r.Data.ProductId))
        {
            if (!products.TryGetValue(group.Key, out var product))
            {
                group.First().Failed("The product was not found.");
                continue;
            }

            var ancestors = SelfAndAncestors(product.Id, parents);

            var ordered = group
                .OrderBy(r => r.Data.StartsOn ?? today)
                .ThenBy(r => r.Data.EndsOn ?? LocalDate.MaxIsoValue)
                .ThenBy(r => r.RowNumber);

            foreach (var row in ordered)
            {
                var applied = Apply(product, row.Data, ancestors, parents, today, actor, timestamp);
                if (applied.IsFailure)
                {
                    row.Failed(applied.Error);
                    break;
                }

                row.Created(applied.Value);
            }
        }

        return Result.Success();
    }

    private static Result<Guid> Apply(
        Product product,
        ImportProductDependencyDto data,
        IReadOnlyCollection<Guid> ancestors,
        Dictionary<Guid, Guid?> parents,
        LocalDate today,
        EventActor actor,
        Instant timestamp)
    {
        if (!parents.ContainsKey(data.DependsOnProductId))
            return Result.Failure<Guid>("The product depended on was not found.");

        var added = product.AddDependency(
            data.DependsOnProductId,
            data.Strength,
            data.Description,
            data.StartsOn ?? today,
            ancestors,
            SelfAndAncestors(data.DependsOnProductId, parents),
            today,
            actor,
            timestamp);

        if (added.IsFailure)
            return Result.Failure<Guid>(added.Error);

        if (data.EndsOn is { } endsOn)
        {
            var ended = product.EndDependency(added.Value.Id, endsOn, today, actor, timestamp);
            if (ended.IsFailure)
                return Result.Failure<Guid>(ended.Error);
        }

        return Result.Success(added.Value.Id);
    }

    /// <summary>
    /// A product and everything above it. Stops on a node already seen, so a catalog that already contains a
    /// cycle cannot loop forever — the domain refuses to create one, so this guards data that is already wrong.
    /// </summary>
    private static HashSet<Guid> SelfAndAncestors(Guid productId, Dictionary<Guid, Guid?> parents)
    {
        HashSet<Guid> chain = [productId];

        var current = parents.GetValueOrDefault(productId);
        while (current is { } parentId && chain.Add(parentId))
            current = parents.GetValueOrDefault(parentId);

        return chain;
    }
}
