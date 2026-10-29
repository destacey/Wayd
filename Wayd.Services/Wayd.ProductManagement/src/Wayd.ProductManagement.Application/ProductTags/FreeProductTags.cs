using Wayd.Common.Domain.Tagging;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.ProductTags;

/// <summary>
/// The lookups and clean-up every command that touches free product tags shares.
/// </summary>
internal static class FreeProductTags
{
    /// <summary>
    /// The free tag with the name, ignoring case, or null. <paramref name="normalizedName"/> must already be
    /// through <see cref="TagBase.ValidateFreeName"/>.
    /// </summary>
    public static Task<ProductTag?> FindByName(IProductManagementDbContext dbContext, string normalizedName,
        CancellationToken cancellationToken)
    {
        var lowered = normalizedName.ToLower();

        return dbContext.ProductTags
            .FirstOrDefaultAsync(t => t.CategoryId == null && t.Name.ToLower() == lowered, cancellationToken);
    }

    /// <summary>
    /// Stages the deletion of each free tag among <paramref name="tagIds"/> that no product other than
    /// <paramref name="changedProductId"/> carries, for the caller's save. Free tags are never kept unused: one
    /// nothing carries is clutter in every picker.
    /// </summary>
    /// <remarks>
    /// Checked against the saved assignments, which still hold the changed product's, so that product is left
    /// out. A product tagged with it between this check and the save makes the save fail on the tag's foreign key,
    /// which the caller reports as a change made meanwhile.
    /// </remarks>
    /// <param name="changedProductId">The product losing the tags, or null when every carrier has already lost them.</param>
    public static async Task DeleteUnused(IProductManagementDbContext dbContext, IReadOnlyCollection<Guid> tagIds,
        Guid? changedProductId, EventActor actor, Instant timestamp, CancellationToken cancellationToken)
    {
        if (tagIds.Count == 0)
            return;

        var freeTags = await dbContext.ProductTags
            .Where(t => tagIds.Contains(t.Id) && t.CategoryId == null)
            .ToListAsync(cancellationToken);

        foreach (var tag in freeTags)
        {
            var carriedElsewhere = await dbContext.ProductTagAssignments
                .AnyAsync(a => a.TagId == tag.Id && a.ProductId != changedProductId, cancellationToken);
            if (carriedElsewhere)
                continue;

            tag.DeleteFreeTag(null, actor, timestamp);
            dbContext.ProductTags.Remove(tag);
        }
    }
}
