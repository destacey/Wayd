using Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Projects.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Queries;

/// <summary>A project's deliverables, in their order. Null when the project does not exist.</summary>
public sealed record GetProjectDeliverablesQuery(ProjectIdOrKey ProjectIdOrKey) : IQuery<IReadOnlyList<ProjectDeliverableDto>?>;

public sealed class GetProjectDeliverablesQueryHandler(IProjectPortfolioManagementDbContext ppmDbContext)
    : IQueryHandler<GetProjectDeliverablesQuery, IReadOnlyList<ProjectDeliverableDto>?>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;

    public async Task<IReadOnlyList<ProjectDeliverableDto>?> Handle(GetProjectDeliverablesQuery request, CancellationToken cancellationToken)
    {
        var projectId = await _ppmDbContext.Projects
            .Where(request.ProjectIdOrKey.CreateFilter<Domain.Models.Project>())
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (projectId is null)
            return null;

        var deliverables = await _ppmDbContext.ProjectDeliverables
            .Where(d => d.ProjectId == projectId)
            .OrderBy(d => d.Order)
            // Id last, so an MCP caller's offset slices the same list on every call.
            .ThenBy(d => d.Id)
            .ProjectToType<ProjectDeliverableDto>()
            .ToListAsync(cancellationToken);

        return await _ppmDbContext.WithProgress(projectId.Value, deliverables, cancellationToken);
    }
}

/// <summary>One deliverable of a project. Null when either does not exist.</summary>
public sealed record GetProjectDeliverableQuery(ProjectIdOrKey ProjectIdOrKey, Guid DeliverableId) : IQuery<ProjectDeliverableDto?>;

public sealed class GetProjectDeliverableQueryHandler(IProjectPortfolioManagementDbContext ppmDbContext)
    : IQueryHandler<GetProjectDeliverableQuery, ProjectDeliverableDto?>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;

    public async Task<ProjectDeliverableDto?> Handle(GetProjectDeliverableQuery request, CancellationToken cancellationToken)
    {
        var projects = _ppmDbContext.Projects.Where(request.ProjectIdOrKey.CreateFilter<Domain.Models.Project>());

        var deliverable = await _ppmDbContext.ProjectDeliverables
            .Where(d => d.Id == request.DeliverableId && projects.Any(p => p.Id == d.ProjectId))
            .ProjectToType<ProjectDeliverableDto>()
            .FirstOrDefaultAsync(cancellationToken);
        if (deliverable is null)
            return null;

        return (await _ppmDbContext.WithProgress(deliverable.ProjectId, [deliverable], cancellationToken)).Single();
    }
}
