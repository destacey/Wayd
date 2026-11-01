using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Planning.Application.EstimationScales.Dtos;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.EstimationScales.Queries;

/// <summary>
/// Get an estimation scale, active or not, with its current version and version history.
/// </summary>
public sealed record GetEstimationScaleQuery : IQuery<EstimationScaleDetailsDto?>
{
    public GetEstimationScaleQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<EstimationScale>();
    }

    public Expression<Func<EstimationScale, bool>> IdOrKeyFilter { get; }
}

public sealed class GetEstimationScaleQueryHandler(IPlanningDbContext planningDbContext) : IQueryHandler<GetEstimationScaleQuery, EstimationScaleDetailsDto?>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;

    public async Task<EstimationScaleDetailsDto?> Handle(GetEstimationScaleQuery request, CancellationToken cancellationToken)
    {
        var scale = await _planningDbContext.EstimationScales
            .AsNoTracking()
            .Include(s => s.Versions)
            .FirstOrDefaultAsync(request.IdOrKeyFilter, cancellationToken);

        return scale is null ? null : EstimationScaleDetailsDto.From(scale);
    }
}
