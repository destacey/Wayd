using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Incidents;

/// <summary>
/// Which incidents still count as active, and which of those is the most severe for a product or release.
/// </summary>
/// <remarks>
/// Active is <see cref="IncidentReadRule.LifecycleOf"/> returning <see cref="LifecycleCategory.Active"/>:
/// the status alias is not Closed or Canceled, and the status category is not Done or Removed. A Done
/// category is completed even when its alias is not Closed, which is narrower than the two aliases alone.
/// Highest severity is the lowest <see cref="IncidentSeverityLevel.SortOrder"/>; a tie breaks on the level id.
/// </remarks>
internal static class IncidentActivity
{
    internal readonly record struct Row(
        Guid OwnerId,
        ProductStatusAlias Alias,
        StatusCategory Category,
        int SortOrder,
        Guid LevelId,
        string Name,
        string Color);

    internal static bool IsActive(ProductStatusAlias alias, StatusCategory category) =>
        IncidentReadRule.LifecycleOf(alias, category) == LifecycleCategory.Active;

    internal static IReadOnlyDictionary<Guid, (string Name, string Color)> HighestByOwner(IEnumerable<Row> rows) =>
        rows.Where(row => IsActive(row.Alias, row.Category))
            .GroupBy(row => row.OwnerId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var top = group.OrderBy(row => row.SortOrder).ThenBy(row => row.LevelId).First();
                    return (top.Name, top.Color);
                });

    internal static async Task<IReadOnlyDictionary<Guid, (string Name, string Color)>> ForProducts(
        IProductManagementDbContext context,
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
        {
            return new Dictionary<Guid, (string Name, string Color)>();
        }

        var raw = await (
            from incident in context.Incidents.AsNoTracking()
            join level in context.IncidentSeverityLevels.AsNoTracking() on incident.SeverityLevelId equals level.Id
            from link in incident.ProductLinks
            where productIds.Contains(link.ProductId)
            select new
            {
                link.ProductId,
                incident.StatusAliasValue,
                incident.StatusCategory,
                level.SortOrder,
                LevelId = level.Id,
                level.Name,
                level.Color,
            }).ToListAsync(cancellationToken);

        return HighestByOwner(raw.Select(row => new Row(
            row.ProductId,
            (ProductStatusAlias)row.StatusAliasValue,
            row.StatusCategory,
            row.SortOrder,
            row.LevelId,
            row.Name,
            row.Color)));
    }

    internal static async Task<IReadOnlyDictionary<Guid, (string Name, string Color)>> ForReleases(
        IProductManagementDbContext context,
        IReadOnlyCollection<Guid> releaseIds,
        CancellationToken cancellationToken)
    {
        if (releaseIds.Count == 0)
        {
            return new Dictionary<Guid, (string Name, string Color)>();
        }

        var raw = await (
            from incident in context.Incidents.AsNoTracking()
            join level in context.IncidentSeverityLevels.AsNoTracking() on incident.SeverityLevelId equals level.Id
            from link in incident.ReleaseLinks
            where releaseIds.Contains(link.ReleaseId)
            select new
            {
                link.ReleaseId,
                incident.StatusAliasValue,
                incident.StatusCategory,
                level.SortOrder,
                LevelId = level.Id,
                level.Name,
                level.Color,
            }).ToListAsync(cancellationToken);

        return HighestByOwner(raw.Select(row => new Row(
            row.ReleaseId,
            (ProductStatusAlias)row.StatusAliasValue,
            row.StatusCategory,
            row.SortOrder,
            row.LevelId,
            row.Name,
            row.Color)));
    }

    internal static void Apply<T>(
        List<T> rows,
        IReadOnlyDictionary<Guid, (string Name, string Color)> severity,
        Func<T, Guid> id,
        Func<T, string, string, T> withSeverity)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            if (severity.TryGetValue(id(rows[index]), out var top))
            {
                rows[index] = withSeverity(rows[index], top.Name, top.Color);
            }
        }
    }
}
