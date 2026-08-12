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
        // Id breaks ties on ChangedOn. Several transitions can share one timestamp — an import walks a
        // project through the real transitions to reach its target status, stamping each with the same
        // instant — and ordering by the timestamp alone would leave those rows in an undefined order.
        // Id is a v7 GUID, so it sorts in insertion order.
        return await _ppmDbContext.ProjectStatusHistory
            .Where(h => h.ProjectId == request.ProjectId)
            .OrderByDescending(h => h.ChangedOn)
            .ThenByDescending(h => h.Id)
            .ProjectToType<ProjectStatusHistoryDto>()
            .ToListAsync(cancellationToken);
    }
}
