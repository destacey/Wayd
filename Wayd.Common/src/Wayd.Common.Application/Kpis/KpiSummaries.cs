using Wayd.Common.Application.Kpis.Dtos;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Kpis;

/// <summary>
/// Builds KPI summaries for the queries: each KPI with its category, its recent values and the reading of the latest.
/// </summary>
/// <remarks>
/// A KPI's history is never loaded to list it: the recent values are read per KPI, newest first and capped, in one
/// query, and the reading is worked out from the last two of them.
/// </remarks>
internal static class KpiSummaries
{
    /// <summary>How many recent values a summary carries for its trend line.</summary>
    public const int RecentValueCount = 12;

    /// <summary>
    /// The KPIs the query selects, as summaries ordered as a scorecard shows them: by category order with
    /// uncategorized last, then position, then key.
    /// </summary>
    public static async Task<List<KpiSummaryDto>> For(IKpisDbContext dbContext, IQueryable<Kpi> kpis, CancellationToken cancellationToken)
    {
        var loaded = await kpis.AsNoTracking().ToListAsync(cancellationToken);
        if (loaded.Count == 0)
            return [];

        var ids = loaded.Select(k => k.Id).ToList();
        var recent = await dbContext.Kpis
            .Where(k => ids.Contains(k.Id))
            .Select(k => new
            {
                k.Id,
                Points = k.Measurements
                    .OrderByDescending(m => m.PeriodEnd)
                    .Take(RecentValueCount)
                    .Select(m => new KpiPointDto(m.PeriodStart, m.PeriodEnd, m.Value))
                    .ToList(),
            })
            .ToDictionaryAsync(x => x.Id, x => x.Points, cancellationToken);

        var categoryIds = loaded.Where(k => k.CategoryId.HasValue).Select(k => k.CategoryId!.Value).Distinct().ToList();
        var categories = categoryIds.Count == 0
            ? new Dictionary<Guid, KpiCategoryRefDto>()
            : await dbContext.KpiCategories
                .Where(c => categoryIds.Contains(c.Id))
                .Select(c => new KpiCategoryRefDto(c.Id, c.Name, c.Order))
                .ToDictionaryAsync(c => c.Id, cancellationToken);

        return loaded
            .Select(k => Summary(k, k.CategoryId is { } c ? categories.GetValueOrDefault(c) : null, recent.GetValueOrDefault(k.Id) ?? []))
            .OrderBy(s => s.Category is null ? 1 : 0)
            .ThenBy(s => s.Category?.Order)
            .ThenBy(s => s.Order)
            .ThenBy(s => s.Key)
            .ToList();
    }

    /// <summary>One KPI's summary, from its newest-first recent values.</summary>
    public static KpiSummaryDto Summary(Kpi kpi, KpiCategoryRefDto? category, IReadOnlyList<KpiPointDto> newestFirst)
    {
        var latest = newestFirst.Count > 0 ? newestFirst[0].Value : (double?)null;
        var previous = newestFirst.Count > 1 ? newestFirst[1].Value : (double?)null;

        return new KpiSummaryDto
        {
            Id = kpi.Id,
            Key = kpi.Key,
            Name = kpi.Name,
            Description = kpi.Description,
            Category = category,
            Prefix = kpi.Prefix,
            Suffix = kpi.Suffix,
            Source = kpi.Source,
            ExpressedAs = kpi.ExpressedAs,
            Status = kpi.Status,
            Order = kpi.Order,
            Goal = KpiGoalDto.From(kpi.Goal),
            Reading = KpiReadingDto.From(KpiReading.Of(kpi.Goal, latest, previous)),
            RecentValues = newestFirst.Reverse().ToList(),
        };
    }
}
