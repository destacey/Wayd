using Wayd.Planning.Application.EstimationScales.Dtos;

namespace Wayd.Planning.Application.EstimationScales.Queries;

/// <summary>
/// List estimation scales with their current versions, active ones only unless <paramref name="IncludeInactive"/>.
/// </summary>
public sealed record GetEstimationScalesQuery(bool IncludeInactive = false) : IQuery<IReadOnlyList<EstimationScaleDto>>;

public sealed class GetEstimationScalesQueryHandler(IPlanningDbContext planningDbContext) : IQueryHandler<GetEstimationScalesQuery, IReadOnlyList<EstimationScaleDto>>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;

    public async Task<IReadOnlyList<EstimationScaleDto>> Handle(GetEstimationScalesQuery request, CancellationToken cancellationToken)
    {
        var query = _planningDbContext.EstimationScales.AsNoTracking();

        if (!request.IncludeInactive)
        {
            query = query.Where(s => s.IsActive);
        }

        // Every version is loaded to find the current one; a scale publishes few enough for that to stay small.
        var scales = await query
            .Include(s => s.Versions)
            .OrderBy(s => s.Name)
            .ThenBy(s => s.Key)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return [.. scales.Select(EstimationScaleDto.From)];
    }
}
