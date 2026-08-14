using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Queries;

public sealed record GetProjectStatusHistoryQuery(Guid ProjectId)
    : IQuery<IReadOnlyList<ProjectStatusHistoryDto>>;

public sealed class GetProjectStatusHistoryQueryHandler(IProjectPortfolioManagementDbContext ppmDbContext)
    : IQueryHandler<GetProjectStatusHistoryQuery, IReadOnlyList<ProjectStatusHistoryDto>>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;

    public async Task<IReadOnlyList<ProjectStatusHistoryDto>> Handle(GetProjectStatusHistoryQuery request, CancellationToken cancellationToken)
    {
        // The rows are materialised rather than projected, because the sequencing below has to inspect
        // each transition's endpoints. That means the acting employee has to be loaded explicitly: a
        // projection would have pulled it in through the mapping, but an entity read will not, and the
        // DTO reads the navigation.
        var history = await _ppmDbContext.ProjectStatusHistory
            .AsNoTracking()
            .Include(h => h.ChangedByEmployee)
            .Where(h => h.ProjectId == request.ProjectId)
            .OrderBy(h => h.ChangedOn)
            .ToListAsync(cancellationToken);

        return [.. Sequence(history).Select(h => h.Adapt<ProjectStatusHistoryDto>())];
    }

    /// <summary>
    /// Orders the history newest first, resolving rows that share a timestamp by following the chain
    /// rather than by any stored value.
    /// </summary>
    /// <remarks>
    /// Transitions can share an instant: an import walks a project through the real transitions to reach
    /// its target status, and every row it writes carries the same timestamp. Neither the timestamp nor
    /// the key can separate those — a v7 GUID only orders by creation time to millisecond precision, so
    /// rows written in one <c>SaveChanges</c> sort arbitrarily among themselves.
    ///
    /// The data orders itself instead. A transition records the status it moved out of, so the row whose
    /// <c>FromStatus</c> matches the previous row's <c>ToStatus</c> is the one that followed it. Walking
    /// that chain from the project's origin (the row with no <c>FromStatus</c>) yields the true sequence.
    /// Anything unreachable — a gap in a reconstructed history — is appended in timestamp order rather
    /// than dropped.
    /// </remarks>
    private static List<ProjectStatusHistory> Sequence(List<ProjectStatusHistory> history)
    {
        if (history.Count < 2)
        {
            return history;
        }

        var remaining = new List<ProjectStatusHistory>(history);
        var ordered = new List<ProjectStatusHistory>(history.Count);

        var current = remaining.FirstOrDefault(h => h.FromStatus is null) ?? remaining[0];

        while (true)
        {
            ordered.Add(current);
            remaining.Remove(current);

            var next = remaining.FirstOrDefault(h => h.FromStatus == current.ToStatus);
            if (next is null)
            {
                break;
            }

            current = next;
        }

        // Rows the chain could not reach keep their timestamp order behind the sequenced ones.
        ordered.AddRange(remaining);
        ordered.Reverse();

        return ordered;
    }
}
