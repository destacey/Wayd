using Wayd.Common.Application.Requests.DemandManagement;

namespace Wayd.DemandManagement.Application.DemandEstimationScales.Queries;

/// <summary>
/// Answers Planning, before it deletes a scale, from Demand Management's desks and estimates.
/// </summary>
public sealed class IsEstimationScaleUsedByDemandQueryHandler(IDemandManagementDbContext dbContext)
    : IQueryHandler<IsEstimationScaleUsedByDemandQuery, bool>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;

    public Task<bool> Handle(IsEstimationScaleUsedByDemandQuery request, CancellationToken cancellationToken) =>
        _dbContext.IsInUse(request.EstimationScaleId, cancellationToken);
}
