using Wayd.Common.Application.Dtos;

namespace Wayd.ProductManagement.Application.Products;

/// <summary>
/// The whole product tree held in memory, for the reads that roll links up a subtree or show where a product
/// sits.
/// </summary>
/// <remarks>
/// The catalog is curated reference data — tens of rows, not thousands — so it is loaded whole and walked
/// here. A recursive CTE would be the alternative, and LINQ does not express one.
/// </remarks>
internal sealed class ProductCatalog
{
    private sealed record Node(Guid Id, int Key, string Name, Guid? ParentId);

    private readonly Dictionary<Guid, Node> _nodes;

    private ProductCatalog(Dictionary<Guid, Node> nodes) => _nodes = nodes;

    public static async Task<ProductCatalog> Load(IProductManagementDbContext productManagementDbContext, CancellationToken cancellationToken)
    {
        var nodes = await productManagementDbContext.Products
            .Select(p => new Node(p.Id, p.Key, p.Name, p.ParentId))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        return new ProductCatalog(nodes);
    }

    /// <summary>The product's name, for ordering rows by it.</summary>
    public string Name(Guid productId) => _nodes[productId].Name;

    /// <summary>The product as a link target: its id, key and name.</summary>
    public NavigationDto Navigation(Guid productId)
    {
        var node = _nodes[productId];
        return NavigationDto.Create(node.Id, node.Key, node.Name);
    }

    /// <summary>Everything a product sits inside, from its root down to its parent.</summary>
    /// <remarks>
    /// Walks up, because a node knows its parent rather than its children, then reverses. Stops on a node
    /// already seen, so a catalog that already contains a cycle cannot loop forever.
    /// </remarks>
    public IReadOnlyList<NavigationDto> Ancestry(Guid productId)
    {
        var ancestry = new List<NavigationDto>();
        var seen = new HashSet<Guid> { productId };
        var parentId = _nodes[productId].ParentId;

        while (parentId is not null && seen.Add(parentId.Value))
        {
            ancestry.Add(Navigation(parentId.Value));
            parentId = _nodes[parentId.Value].ParentId;
        }

        ancestry.Reverse();

        return ancestry;
    }

    /// <summary>A node and everything beneath it.</summary>
    /// <remarks>
    /// Tracks what it has seen so data that already contains a cycle cannot loop forever. The domain
    /// refuses to create one, so this guards against a catalog that is already wrong.
    /// </remarks>
    public HashSet<Guid> Subtree(Guid root)
    {
        var childrenByParent = _nodes.Values
            .Where(p => p.ParentId is not null)
            .ToLookup(p => p.ParentId!.Value, p => p.Id);

        var subtree = new HashSet<Guid> { root };
        var pending = new Queue<Guid>([root]);

        while (pending.TryDequeue(out var id))
        {
            foreach (var child in childrenByParent[id].Where(subtree.Add))
            {
                pending.Enqueue(child);
            }
        }

        return subtree;
    }
}
