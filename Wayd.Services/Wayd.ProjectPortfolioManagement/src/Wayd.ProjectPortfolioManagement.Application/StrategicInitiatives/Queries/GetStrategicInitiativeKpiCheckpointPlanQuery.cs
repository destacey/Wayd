using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;

namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Queries;

public sealed record GetStrategicInitiativeKpiCheckpointPlanQuery : IQuery<List<StrategicInitiativeKpiCheckpointDetailsDto>?>
{
    public GetStrategicInitiativeKpiCheckpointPlanQuery(IdOrKey strategicInitiativeIdOrKey, IdOrKey kpiIdOrKey)
    {
        StrategicInitiativeIdOrKeyFilter = strategicInitiativeIdOrKey.CreateFilter<StrategicInitiative>();
        KpiIdOrKey = kpiIdOrKey;
    }

    public Expression<Func<StrategicInitiative, bool>> StrategicInitiativeIdOrKeyFilter { get; }

    public IdOrKey KpiIdOrKey { get; }
}

public sealed class GetStrategicInitiativeKpiCheckpointPlanQueryHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext)
    : IQueryHandler<GetStrategicInitiativeKpiCheckpointPlanQuery, List<StrategicInitiativeKpiCheckpointDetailsDto>?>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = projectPortfolioManagementDbContext;

    public async Task<List<StrategicInitiativeKpiCheckpointDetailsDto>?> Handle(GetStrategicInitiativeKpiCheckpointPlanQuery request, CancellationToken cancellationToken)
    {
        var strategicInitiativeQuery = _ppmDbContext.StrategicInitiatives
            .Where(request.StrategicInitiativeIdOrKeyFilter);

        if (await strategicInitiativeQuery.AnyAsync(cancellationToken) == false)
        {
            return null;
        }

        var kpiQuery = GetKpiQuery(strategicInitiativeQuery, request.KpiIdOrKey);

        var kpi = await kpiQuery
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        if (kpi is null)
        {
            return null;
        }

        var checkpoints = await kpiQuery
            .SelectMany(k => k.Checkpoints)
            .ProjectToType<StrategicInitiativeKpiCheckpointDetailsDto>()
            .OrderBy(c => c.CheckpointDate)
            .ToListAsync(cancellationToken);

        if (checkpoints.Count == 0)
        {
            return [];
        }

        var measurements = await kpiQuery
            .SelectMany(k => k.Measurements)
            .ProjectToType<StrategicInitiativeKpiMeasurementDto>()
            .ToListAsync(cancellationToken);

        StrategicInitiativeKpiCheckpointDetailsDto? previousCheckpoint = null;

        foreach (var checkpoint in checkpoints)
        {
            var measurement = measurements
                .Where(m => m.MeasurementDate <= checkpoint.CheckpointDate
                    && (previousCheckpoint == null || m.MeasurementDate > previousCheckpoint.CheckpointDate))
                .OrderByDescending(m => m.MeasurementDate)
                .ThenByDescending(m => m.Id)
                .FirstOrDefault();

            if (measurement is not null)
            {
                checkpoint.Enrich(measurement!, previousCheckpoint?.Measurement, kpi.TargetDirection);
            }

            previousCheckpoint = checkpoint;
        }

        return checkpoints;
    }

    private static IQueryable<StrategicInitiativeKpi> GetKpiQuery(IQueryable<StrategicInitiative> query, IdOrKey kpiIdOrKey)
    {
        return kpiIdOrKey.IsId
            ? query.SelectMany(i => i.Kpis.Where(k => k.Id == kpiIdOrKey.AsId))
            : query.SelectMany(i => i.Kpis.Where(k => k.Key == kpiIdOrKey.AsKey));
    }
}
