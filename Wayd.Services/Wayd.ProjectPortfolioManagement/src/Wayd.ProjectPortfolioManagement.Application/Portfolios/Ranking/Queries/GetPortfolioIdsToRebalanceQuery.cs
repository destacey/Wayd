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

internal sealed class GetPortfolioIdsToRebalanceQueryHandler(IProjectPortfolioManagementDbContext ppmDbContext)
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
        // Compute the gap between each project's rank and the next-lower rank within its portfolio
        // (window function — not expressible in LINQ), then select portfolios with any gap below the
        // threshold. Status is persisted as a varchar enum name, so compare against the name.
        var archivedName = ProjectPortfolioStatus.Archived.ToString();

        var ids = await _ppmDbContext.Database
            .SqlQuery<Guid>($@"
                WITH Gaps AS (
                    SELECT
                        prj.PortfolioId,
                        prj.Rank - LAG(prj.Rank) OVER (PARTITION BY prj.PortfolioId ORDER BY prj.Rank) AS Gap
                    FROM Ppm.Projects prj
                )
                SELECT pf.Id AS Value
                FROM Ppm.Portfolios pf
                WHERE pf.Status <> {archivedName}
                  AND EXISTS (
                      SELECT 1 FROM Gaps g
                      WHERE g.PortfolioId = pf.Id AND g.Gap IS NOT NULL AND g.Gap < {MinGapThreshold}
                  )")
            .ToListAsync(cancellationToken);

        return ids;
    }
}
