using Wayd.Common.Application.Requests.Planning.Queries;
using Wayd.Common.Domain.Events.Planning.EstimationScales;
using Wayd.Common.Domain.Models.Planning.EstimationScales;

namespace Wayd.Planning.Application.EstimationScales.Queries;

/// <summary>
/// Answers Demand Management, which keeps a copy of the scales, with Planning's scales and every version.
/// </summary>
public sealed class GetEstimationScaleSnapshotsQueryHandler(IPlanningDbContext planningDbContext)
    : IQueryHandler<GetEstimationScaleSnapshotsQuery, IReadOnlyList<EstimationScaleSnapshot>>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;

    public async Task<IReadOnlyList<EstimationScaleSnapshot>> Handle(GetEstimationScaleSnapshotsQuery request, CancellationToken cancellationToken)
    {
        var query = _planningDbContext.EstimationScales.AsNoTracking().Include(s => s.Versions).AsQueryable();
        if (request.Id is { } id)
            query = query.Where(s => s.Id == id);

        var scales = await query.OrderBy(s => s.Key).ToListAsync(cancellationToken);

        return [.. scales.Select(s => new EstimationScaleSnapshot(
            s.Id,
            s.Key,
            s.Name,
            s.IsActive,
            [.. s.Versions
                .OrderBy(v => v.Number)
                .Select(v => new EstimationScaleVersionValues(
                    v.Id,
                    v.Number,
                    [.. v.Values.Select(x => new EstimationScaleValueValues(x.Value, x.IsEstimate))]))]))];
    }
}
