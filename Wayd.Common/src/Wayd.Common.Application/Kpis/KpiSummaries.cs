using Wayd.Common.Application.Kpis.Dtos;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Kpis;

/// <summary>
/// Builds KPI summaries for the queries: each KPI with its category, its recent values and the reading of the latest.
/// </summary>
/// <remarks>
/// A KPI's history is never loaded to list it: the recent values are read per KPI, newest first and capped, in one
/// query, and the reading is worked out from them — the last two, or as many as its rolling average needs.
/// </remarks>
internal static class KpiSummaries
{
    /// <summary>How many recent values a summary carries for its trend line.</summary>
    public const int RecentValueCount = 12;

    /// <summary>
    /// How many values are read per KPI: enough for the trend line, and for the largest rolling average and the one
    /// before it.
    /// </summary>
    public const int ReadValueCount = Kpi.MaxRollingAverageOf + 1 > RecentValueCount ? Kpi.MaxRollingAverageOf + 1 : RecentValueCount;

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
                    .ThenByDescending(m => m.MeasuredAt)
                    .Take(ReadValueCount)
                    .Select(m => new KpiPointDto(m.PeriodStart, m.PeriodEnd, m.Value, m.MeasuredAt))
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

    /// <summary>One KPI's summary, from its newest-first recent values — up to <see cref="ReadValueCount"/> of them.</summary>
    public static KpiSummaryDto Summary(Kpi kpi, KpiCategoryRefDto? category, IReadOnlyList<KpiPointDto> newestFirst)
    {
        var reading = KpiReading.OfValues(kpi.Goal, newestFirst.Select(p => p.Value).ToList(), kpi.RollingAverageOf);

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
            Window = KpiMeasurementWindowDto.From(kpi.Window),
            RollingAverageOf = kpi.RollingAverageOf,
            Status = kpi.Status,
            Order = kpi.Order,
            Goal = KpiGoalDto.From(kpi.Goal),
            Reading = KpiReadingDto.From(reading),
            RecentValues = newestFirst.Take(RecentValueCount).Reverse().ToList(),
        };
    }
}
