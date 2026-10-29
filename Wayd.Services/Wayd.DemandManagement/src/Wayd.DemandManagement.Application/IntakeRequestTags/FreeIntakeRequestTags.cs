using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Tagging;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequestTags;

/// <summary>
/// The lookups and clean-up every command that touches free intake request tags shares.
/// </summary>
internal static class FreeIntakeRequestTags
{
    /// <summary>
    /// The free tag with the name, ignoring case, or null. <paramref name="normalizedName"/> must already be
    /// through <see cref="TagBase.ValidateFreeName"/>.
    /// </summary>
    public static Task<IntakeRequestTag?> FindByName(IDemandManagementDbContext dbContext, string normalizedName,
        CancellationToken cancellationToken)
    {
        var lowered = normalizedName.ToLower();

        return dbContext.IntakeRequestTags
            .FirstOrDefaultAsync(t => t.CategoryId == null && t.Name.ToLower() == lowered, cancellationToken);
    }

    /// <summary>
    /// Stages the deletion of each free tag among <paramref name="tagIds"/> that no request other than
    /// <paramref name="changedRequestId"/> carries, on any desk, for the caller's save. Free tags are never kept
    /// unused: one nothing carries is clutter in every picker.
    /// </summary>
    /// <remarks>
    /// Checked against saved requests, which still hold the changed request's tags, so that request is left out. A
    /// request given the tag between this check and the save makes the save fail on the tag's foreign key, which
    /// the caller reports as a change made meanwhile.
    /// </remarks>
    public static async Task DeleteUnused(IDemandManagementDbContext dbContext, IReadOnlyCollection<Guid> tagIds,
        Guid changedRequestId, EventActor actor, Instant timestamp, CancellationToken cancellationToken)
    {
        if (tagIds.Count == 0)
            return;

        var freeTags = await dbContext.IntakeRequestTags
            .Where(t => tagIds.Contains(t.Id) && t.CategoryId == null)
            .ToListAsync(cancellationToken);

        foreach (var tag in freeTags)
        {
            var carriedElsewhere = await dbContext.IntakeRequests
                .AnyAsync(r => r.Id != changedRequestId && r.Tags.Any(a => a.TagId == tag.Id), cancellationToken);
            if (carriedElsewhere)
                continue;

            tag.DeleteFreeTag(null, actor, timestamp);
            dbContext.IntakeRequestTags.Remove(tag);
        }
    }
}
