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
    /// The free tag with the name, created and saved when none has it yet. <paramref name="normalizedName"/> must
    /// already be through <see cref="TagBase.ValidateFreeName"/>.
    /// </summary>
    /// <remarks>
    /// Created in a save of its own, before the product is tagged, so that someone creating the same name at
    /// the same moment costs a second lookup rather than the caller's change: the unique index refuses the later
    /// insert, and that caller then finds and uses the tag that won.
    /// </remarks>
    public static async Task<Result<ProductTag>> FindOrCreate(IProductManagementDbContext dbContext, string normalizedName,
        EventActor actor, Instant timestamp, CancellationToken cancellationToken)
    {
        if (await FindByName(dbContext, normalizedName, cancellationToken) is { } existing)
            return existing;

        var created = ProductTag.CreateFree(normalizedName, actor, timestamp);
        if (created.IsFailure)
            return created;

        dbContext.ProductTags.Add(created.Value);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return created.Value;
        }
        catch (DbUpdateException)
        {
            // Removing an entity that never saved detaches it, so the next save does not try the insert again.
            dbContext.ProductTags.Remove(created.Value);

            return await FindByName(dbContext, normalizedName, cancellationToken) is { } winner
                ? winner
                : Result.Failure<ProductTag>(ProductTagErrors.ChangedWhileSaving);
        }
    }

    /// <summary>
    /// Deletes each free tag among <paramref name="tagIds"/> that no product carries, in a save of its own. Free
    /// tags are never kept unused: one nothing carries is clutter in every picker.
    /// </summary>
    /// <remarks>
    /// Called after the change that removed the tags has been saved, never in the same save. Checked then, two
    /// people removing a tag's last two uses at once both see the other's removal committed, so one of them deletes
    /// it; checked before, each would see the other's still there and neither would. Kept out of the change's own
    /// save so that a product given the tag meanwhile fails only this clean-up — which is then right to keep the
    /// tag — and never the change the caller asked for. A failed clean-up is therefore not an error.
    /// </remarks>
    public static async Task DeleteUnused(IProductManagementDbContext dbContext, IReadOnlyCollection<Guid> tagIds,
        EventActor actor, Instant timestamp, CancellationToken cancellationToken)
    {
        if (tagIds.Count == 0)
            return;

        var freeTags = await dbContext.ProductTags
            .Where(t => tagIds.Contains(t.Id) && t.CategoryId == null)
            .ToListAsync(cancellationToken);

        var deleted = false;
        foreach (var tag in freeTags)
        {
            if (await dbContext.Products.AnyAsync(p => p.Tags.Any(a => a.TagId == tag.Id), cancellationToken))
                continue;

            tag.DeleteFreeTag(null, actor, timestamp);
            dbContext.ProductTags.Remove(tag);
            deleted = true;
        }

        if (!deleted)
            return;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A product was given the tag after the check, or another clean-up deleted it first: either way the
            // tag is where it should be.
        }
    }
}
