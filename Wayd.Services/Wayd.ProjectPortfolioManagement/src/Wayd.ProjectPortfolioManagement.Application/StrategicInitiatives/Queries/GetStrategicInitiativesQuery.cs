using Wayd.Common.Application.Models;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Queries;

/// <summary>
/// Retrieves a list of strategic initiatives based on the provided filter.  Returns all strategic initiatives if no filter is provided.
/// </summary>
/// <param name="StatusFilter"></param>
/// <param name="PortfolioIdOrKey"></param>
public sealed record GetStrategicInitiativesQuery(StrategicInitiativeStatus[]? StatusFilter = null, IdOrKey? PortfolioIdOrKey = null) : IQuery<List<StrategicInitiativeListDto>>;

public sealed class GetStrategicInitiativesQueryHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext)
    : IQueryHandler<GetStrategicInitiativesQuery, List<StrategicInitiativeListDto>>
{
    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;

    public async Task<List<StrategicInitiativeListDto>> Handle(GetStrategicInitiativesQuery request, CancellationToken cancellationToken)
    {
        var query = _projectPortfolioManagementDbContext.StrategicInitiatives.AsQueryable();

        if (request.StatusFilter is { Length: > 0 })
        {
            query = query.Where(si => request.StatusFilter.Contains(si.Status));
        }

        if (request.PortfolioIdOrKey is not null)
        {
            // TODO: make this reusable
            Guid? portfolioId = request.PortfolioIdOrKey.IsId
                ? request.PortfolioIdOrKey.AsId
                : await _projectPortfolioManagementDbContext.Portfolios
                    .Where(p => p.Key == request.PortfolioIdOrKey.AsKey)
                    .Select(p => (Guid?)p.Id)
                    .FirstOrDefaultAsync(cancellationToken);

            if (portfolioId is null)
            {
                return [];
            }

            query = query.Where(pp => pp.PortfolioId == portfolioId);
        }

        return await query.ProjectToType<StrategicInitiativeListDto>().ToListAsync(cancellationToken);
    }
}
