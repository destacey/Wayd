using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;

namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Queries;

public sealed record GetStrategicInitiativeKpiCheckpointsQuery : IQuery<List<StrategicInitiativeKpiCheckpointDto>?>
{
    public GetStrategicInitiativeKpiCheckpointsQuery(IdOrKey strategicInitiativeIdOrKey, IdOrKey kpiIdOrKey)
    {
        StrategicInitiativeIdOrKeyFilter = strategicInitiativeIdOrKey.CreateFilter<StrategicInitiative>();
        KpiIdOrKey = kpiIdOrKey;
    }

    public Expression<Func<StrategicInitiative, bool>> StrategicInitiativeIdOrKeyFilter { get; }

    public IdOrKey KpiIdOrKey { get; }
}

public sealed class GetStrategicInitiativeKpiCheckpointsQueryHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext)
    : IQueryHandler<GetStrategicInitiativeKpiCheckpointsQuery, List<StrategicInitiativeKpiCheckpointDto>?>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = projectPortfolioManagementDbContext;

    public async Task<List<StrategicInitiativeKpiCheckpointDto>?> Handle(GetStrategicInitiativeKpiCheckpointsQuery request, CancellationToken cancellationToken)
    {
        var strategicInitiativeQuery = _ppmDbContext.StrategicInitiatives
            .Where(request.StrategicInitiativeIdOrKeyFilter);

        if (await strategicInitiativeQuery.AnyAsync(cancellationToken) == false)
        {
            return null;
        }

        var kpiQuery = GetKpiQuery(strategicInitiativeQuery, request.KpiIdOrKey);

        if (await kpiQuery.AnyAsync(cancellationToken) == false)
        {
            return null;
        }

        return await kpiQuery
            .SelectMany(k => k.Checkpoints)
            .ProjectToType<StrategicInitiativeKpiCheckpointDto>()
            .OrderBy(c => c.CheckpointDate)
            .ToListAsync(cancellationToken);
    }

    private static IQueryable<StrategicInitiativeKpi> GetKpiQuery(IQueryable<StrategicInitiative> query, IdOrKey kpiIdOrKey)
    {
        return kpiIdOrKey.IsId
            ? query.SelectMany(i => i.Kpis.Where(k => k.Id == kpiIdOrKey.AsId))
            : query.SelectMany(i => i.Kpis.Where(k => k.Key == kpiIdOrKey.AsKey));
    }
}
