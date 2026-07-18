using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;

namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Queries;

public sealed record GetStrategicInitiativeKpisQuery : IQuery<List<StrategicInitiativeKpiListDto>?>
{
    public GetStrategicInitiativeKpisQuery(IdOrKey strategicInitiativeIdOrKey)
    {
        StrategicInitiativeIdOrKeyFilter = strategicInitiativeIdOrKey.CreateFilter<StrategicInitiative>();
    }

    public Expression<Func<StrategicInitiative, bool>> StrategicInitiativeIdOrKeyFilter { get; }
}

public sealed class GetStrategicInitiativeKpisQueryHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext)
    : IQueryHandler<GetStrategicInitiativeKpisQuery, List<StrategicInitiativeKpiListDto>?>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = projectPortfolioManagementDbContext;

    public async Task<List<StrategicInitiativeKpiListDto>?> Handle(GetStrategicInitiativeKpisQuery request, CancellationToken cancellationToken)
    {
        var query = _ppmDbContext.StrategicInitiatives
            .Where(request.StrategicInitiativeIdOrKeyFilter);

        if (await query.AnyAsync(cancellationToken) == false)
        {
            return null;
        }

        var kpis = await query
            .SelectMany(i => i.Kpis)
            .ProjectToType<StrategicInitiativeKpiListDto>()
            .ToListAsync(cancellationToken);

        return [.. kpis.OrderBy(k => k.Order)];
    }
}
