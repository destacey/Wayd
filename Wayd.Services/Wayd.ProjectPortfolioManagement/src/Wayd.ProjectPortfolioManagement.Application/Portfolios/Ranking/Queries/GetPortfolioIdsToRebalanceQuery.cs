using Microsoft.EntityFrameworkCore;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.Portfolios.Ranking.Queries;

/// <summary>
/// Returns the ids of portfolios that actually need a scheduled rank rebalance: not archived
/// (archived portfolios are read-only) and where at least one pair of adjacent project ranks has
/// drifted closer than <see cref="MinGapThreshold"/> — i.e. inserts in that gap are running low on
/// fractional headroom. Portfolios whose ranks are still well-spaced are skipped, so the recurring
/// job only rewrites portfolios that benefit from it.
/// </summary>
public sealed record GetPortfolioIdsToRebalanceQuery : IQuery<List<Guid>>;

public sealed class GetPortfolioIdsToRebalanceQueryHandler(IProjectPortfolioManagementDbContext ppmDbContext)
    : IQueryHandler<GetPortfolioIdsToRebalanceQuery, List<Guid>>
{
    // Adjacent ranks start RankStep (1000) apart and halve on each midpoint insert into the same gap
    // (1000 -> 500 -> 250 ...). A double survives ~52 such bisections before precision fails, so a
    // threshold of 1.0 (~10 bisections in from 1000) triggers a rebalance with enormous margin to
    // spare while leaving ordinary, well-spaced boards untouched.
    private const double MinGapThreshold = 1.0d;

    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;

    public async Task<List<Guid>> Handle(GetPortfolioIdsToRebalanceQuery request, CancellationToken cancellationToken)
    {
        // The gap between adjacent ranks is a window function, which LINQ cannot express; the ranks of
        // every live portfolio are a few thousand doubles, so they are sorted and compared here instead.
        var ranks = await _ppmDbContext.Projects
            .Join(_ppmDbContext.Portfolios.Where(pf => pf.Status != ProjectPortfolioStatus.Archived),
                prj => prj.PortfolioId,
                pf => pf.Id,
                (prj, pf) => new { prj.PortfolioId, prj.Rank })
            .ToListAsync(cancellationToken);

        return ranks
            .GroupBy(r => r.PortfolioId)
            .Where(portfolio => HasGapBelowThreshold(portfolio.Select(r => r.Rank)))
            .Select(portfolio => portfolio.Key)
            .ToList();
    }

    private static bool HasGapBelowThreshold(IEnumerable<double> ranks)
    {
        var sorted = ranks.Order().ToList();
        for (var i = 1; i < sorted.Count; i++)
        {
            if (sorted[i] - sorted[i - 1] < MinGapThreshold)
                return true;
        }
        return false;
    }
}
