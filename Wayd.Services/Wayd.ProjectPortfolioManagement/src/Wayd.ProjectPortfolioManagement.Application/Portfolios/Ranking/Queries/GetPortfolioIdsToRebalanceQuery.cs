using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.Portfolios.Ranking.Queries;

/// <summary>
/// Returns the ids of portfolios eligible for a scheduled rank rebalance: not archived (archived
/// portfolios are read-only) and containing at least one project (nothing to rebalance otherwise).
/// </summary>
public sealed record GetPortfolioIdsToRebalanceQuery : IQuery<List<Guid>>;

internal sealed class GetPortfolioIdsToRebalanceQueryHandler(IProjectPortfolioManagementDbContext ppmDbContext)
    : IQueryHandler<GetPortfolioIdsToRebalanceQuery, List<Guid>>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;

    public async Task<List<Guid>> Handle(GetPortfolioIdsToRebalanceQuery request, CancellationToken cancellationToken)
    {
        return await _ppmDbContext.Portfolios
            .AsNoTracking()
            .Where(p => p.Status != ProjectPortfolioStatus.Archived
                && p.Projects.Any())
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);
    }
}
