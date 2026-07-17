using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;

namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Queries;

public sealed record GetStrategicInitiativeQuery : IQuery<StrategicInitiativeDetailsDto?>
{
    public GetStrategicInitiativeQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<StrategicInitiative>();
    }

    public Expression<Func<StrategicInitiative, bool>> IdOrKeyFilter { get; }
}

public sealed class GetStrategicInitiativeQueryHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext)
    : IQueryHandler<GetStrategicInitiativeQuery, StrategicInitiativeDetailsDto?>
{
    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    public async Task<StrategicInitiativeDetailsDto?> Handle(GetStrategicInitiativeQuery request, CancellationToken cancellationToken)
    {
        return await _projectPortfolioManagementDbContext.StrategicInitiatives
            .Where(request.IdOrKeyFilter)
            .ProjectToType<StrategicInitiativeDetailsDto>()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
