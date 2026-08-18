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
        // Order by Sequence, not ChangedOn: an import walks a project through several transitions in one
        // SaveChanges, so those rows share an instant, and a v7 GUID only orders to millisecond precision.
        //
        // Materialised rather than projected, because the DTO mapping calls into
        // NavigationDto/LifecycleNavigationDto, which do not translate to SQL. The acting employee
        // therefore has to be included explicitly — the DTO reads that navigation.
        var history = await _ppmDbContext.ProjectStatusHistory
            .AsNoTracking()
            .Include(h => h.ChangedByEmployee)
            .Where(h => h.ProjectId == request.ProjectId)
            .OrderByDescending(h => h.Sequence)
            .ToListAsync(cancellationToken);

        return [.. history.Select(h => h.Adapt<ProjectStatusHistoryDto>())];
    }
}
