using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Incidents.Dtos;
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
        string Color,
        int IncidentKey,
        string Title,
        string StatusName);

    /// <summary>One active incident a product or release row can open from the count pill.</summary>
    internal readonly record struct ActiveIncidentLink(
        int Key,
        string Title,
        string SeverityName,
        string SeverityColor,
        string StatusName);

    /// <summary>The highest active severity for one owner, and every active incident that owner has.</summary>
    internal readonly record struct ActiveSeverity(
        string Name,
        string Color,
        IReadOnlyList<ActiveIncidentLink> Incidents)
    {
        public List<ActiveIncidentLinkDto> ToLinkDtos() =>
            Incidents.Select(incident => new ActiveIncidentLinkDto
            {
                Key = incident.Key,
                Title = incident.Title,
                SeverityName = incident.SeverityName,
                SeverityColor = incident.SeverityColor,
                StatusName = incident.StatusName,
            }).ToList();
    }

    internal static bool IsActive(ProductStatusAlias alias, StatusCategory category) =>
        IncidentReadRule.LifecycleOf(alias, category) == LifecycleCategory.Active;

    internal static IReadOnlyDictionary<Guid, ActiveSeverity> HighestByOwner(IEnumerable<Row> rows) =>
        rows.Where(row => IsActive(row.Alias, row.Category))
            .GroupBy(row => row.OwnerId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var ordered = group
                        .OrderBy(row => row.SortOrder)
                        .ThenBy(row => row.LevelId)
                        .ThenBy(row => row.IncidentKey)
                        .ToList();
                    var top = ordered[0];
                    return new ActiveSeverity(
                        top.Name,
                        top.Color,
                        ordered.Select(row => new ActiveIncidentLink(
                            row.IncidentKey,
                            row.Title,
                            row.Name,
                            row.Color,
                            row.StatusName)).ToList());
                });

    internal static async Task<IReadOnlyDictionary<Guid, ActiveSeverity>> ForProducts(
        IProductManagementDbContext context,
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
        {
            return new Dictionary<Guid, ActiveSeverity>();
        }

        var raw = await (
            from incident in context.Incidents.AsNoTracking()
            join level in context.IncidentSeverityLevels.AsNoTracking() on incident.SeverityLevelId equals level.Id
            from link in incident.ProductLinks
            where productIds.Contains(link.ProductId)
            select new
            {
                link.ProductId,
                incident.Key,
                incident.Title,
                incident.StatusName,
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
            row.Color,
            row.Key,
            row.Title,
            row.StatusName)));
    }

    internal static async Task<IReadOnlyDictionary<Guid, ActiveSeverity>> ForReleases(
        IProductManagementDbContext context,
        IReadOnlyCollection<Guid> releaseIds,
        CancellationToken cancellationToken)
    {
        if (releaseIds.Count == 0)
        {
            return new Dictionary<Guid, ActiveSeverity>();
        }

        var raw = await (
            from incident in context.Incidents.AsNoTracking()
            join level in context.IncidentSeverityLevels.AsNoTracking() on incident.SeverityLevelId equals level.Id
            from link in incident.ReleaseLinks
            where releaseIds.Contains(link.ReleaseId)
            select new
            {
                link.ReleaseId,
                incident.Key,
                incident.Title,
                incident.StatusName,
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
            row.Color,
            row.Key,
            row.Title,
            row.StatusName)));
    }

    internal static void Apply<T>(
        List<T> rows,
        IReadOnlyDictionary<Guid, ActiveSeverity> severity,
        Func<T, Guid> id,
        Func<T, ActiveSeverity, T> withSeverity)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            if (severity.TryGetValue(id(rows[index]), out var top))
            {
                rows[index] = withSeverity(rows[index], top);
            }
        }
    }
}
