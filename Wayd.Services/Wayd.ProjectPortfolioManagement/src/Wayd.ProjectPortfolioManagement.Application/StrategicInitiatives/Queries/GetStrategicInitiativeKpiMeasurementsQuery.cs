using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;

namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Queries;

public sealed record GetStrategicInitiativeKpiMeasurementsQuery : IQuery<List<StrategicInitiativeKpiMeasurementDto>?>
{
    public GetStrategicInitiativeKpiMeasurementsQuery(IdOrKey strategicInitiativeIdOrKey, IdOrKey kpiIdOrKey)
    {
        StrategicInitiativeIdOrKeyFilter = strategicInitiativeIdOrKey.CreateFilter<StrategicInitiative>();
        KpiIdOrKey = kpiIdOrKey;
    }

    public Expression<Func<StrategicInitiative, bool>> StrategicInitiativeIdOrKeyFilter { get; }

    public IdOrKey KpiIdOrKey { get; }
}

public sealed class GetStrategicInitiativeKpiMeasurementsQueryHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext)
    : IQueryHandler<GetStrategicInitiativeKpiMeasurementsQuery, List<StrategicInitiativeKpiMeasurementDto>?>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = projectPortfolioManagementDbContext;

    public async Task<List<StrategicInitiativeKpiMeasurementDto>?> Handle(GetStrategicInitiativeKpiMeasurementsQuery request, CancellationToken cancellationToken)
    {
        var strategicInitiativeQuery = _ppmDbContext.StrategicInitiatives
            .Where(request.StrategicInitiativeIdOrKeyFilter);

        if (await strategicInitiativeQuery.AnyAsync(cancellationToken) == false)
        {
            return null;
        }

        var kpiQuery = request.KpiIdOrKey.IsId
            ? strategicInitiativeQuery.SelectMany(i => i.Kpis.Where(k => k.Id == request.KpiIdOrKey.AsId))
            : strategicInitiativeQuery.SelectMany(i => i.Kpis.Where(k => k.Key == request.KpiIdOrKey.AsKey));

        if (await kpiQuery.AnyAsync(cancellationToken) == false)
        {
            return null;
        }

        return await kpiQuery
            .SelectMany(k => k.Measurements)
            .ProjectToType<StrategicInitiativeKpiMeasurementDto>()
            .OrderByDescending(m => m.MeasurementDate)
            .ToListAsync(cancellationToken);
    }
}
