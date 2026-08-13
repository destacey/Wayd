using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Queries;

public sealed record GetProjectStatusHistoryQuery(Guid ProjectId)
    : IQuery<IReadOnlyList<ProjectStatusHistoryDto>>;

public sealed class GetProjectStatusHistoryQueryHandler(IProjectPortfolioManagementDbContext ppmDbContext)
    : IQueryHandler<GetProjectStatusHistoryQuery, IReadOnlyList<ProjectStatusHistoryDto>>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;

    public async Task<IReadOnlyList<ProjectStatusHistoryDto>> Handle(GetProjectStatusHistoryQuery request, CancellationToken cancellationToken)
    {
        // Several transitions can share one timestamp — an import walks a project through the real
        // transitions to reach its target status, stamping each with the same instant — so ChangedOn
        // alone leaves those rows in an undefined order.
        //
        // The origin row (no FromStatus) is pinned last in this descending order, since a project can
        // only enter its initial state once. Id then separates the rest: it is a v7 GUID for rows the
        // application wrote, so it sorts in insertion order. Seeded rows carry no ordering in their key,
        // but they are reconstructed one-per-audit-entry and those timestamps are distinct, so the
        // tie-break is not load-bearing for them.
        return await _ppmDbContext.ProjectStatusHistory
            .Where(h => h.ProjectId == request.ProjectId)
            .OrderByDescending(h => h.ChangedOn)
            .ThenBy(h => h.FromStatus == null)
            .ThenByDescending(h => h.Id)
            .ProjectToType<ProjectStatusHistoryDto>()
            .ToListAsync(cancellationToken);
    }
}
