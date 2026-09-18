namespace Wayd.ProductManagement.Application.Products;

/// <summary>
/// Walks the product tree upward, for the domain checks that need a node's ancestry handed to them.
/// </summary>
internal static class ProductAncestry
{
    /// <summary>
    /// A node and every ancestor above it, the node first and the root last.
    /// </summary>
    /// <remarks>
    /// Iterative rather than a recursive CTE because the tree is small and this stays provider-agnostic.
    /// The visited set bounds it even if existing data already holds a cycle.
    /// </remarks>
    public static async Task<Result<IReadOnlyCollection<Guid>>> SelfAndAncestors(
        this IProductManagementDbContext productManagementDbContext, Guid startId, CancellationToken cancellationToken)
    {
        var ancestors = new List<Guid>();
        var visited = new HashSet<Guid>();
        Guid? currentId = startId;

        while (currentId is not null)
        {
            if (!visited.Add(currentId.Value))
            {
                return Result.Failure<IReadOnlyCollection<Guid>>("The product hierarchy contains a cycle and must be corrected first.");
            }

            ancestors.Add(currentId.Value);

            // Projected into a wrapper so a root (null ParentId) is distinguishable from a missing
            // row — both come back as default from a bare Guid? projection.
            var nodeId = currentId.Value;
            var parent = await productManagementDbContext.Products
                .Where(p => p.Id == nodeId)
                .Select(p => new { p.ParentId })
                .FirstOrDefaultAsync(cancellationToken);

            currentId = parent?.ParentId;
        }

        return Result.Success<IReadOnlyCollection<Guid>>(ancestors);
    }
}
