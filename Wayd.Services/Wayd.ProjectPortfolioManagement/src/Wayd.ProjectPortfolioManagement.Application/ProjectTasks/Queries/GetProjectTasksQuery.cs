using System.Linq.Expressions;
using Wayd.ProjectPortfolioManagement.Application.Projects.Models;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Queries;

public sealed record GetProjectTasksQuery : IQuery<IReadOnlyList<ProjectTaskListDto>>
{
    public GetProjectTasksQuery(
        ProjectIdOrKey projectIdOrKey,
        Domain.Enums.TaskStatus? statusFilter = null,
        Guid? parentId = null
    )
    {
        ProjectIdOrKeyFilter = projectIdOrKey.CreateProjectFilter<ProjectTask>();
        StatusFilter = statusFilter;
        ParentId = parentId;
    }

    public Expression<Func<ProjectTask, bool>> ProjectIdOrKeyFilter { get; }
    public Domain.Enums.TaskStatus? StatusFilter { get; }
    public Guid? ParentId { get; }
}

public sealed class GetProjectTasksQueryHandler(IProjectPortfolioManagementDbContext ppmDbContext)
    : IQueryHandler<GetProjectTasksQuery, IReadOnlyList<ProjectTaskListDto>>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;

    public async Task<IReadOnlyList<ProjectTaskListDto>> Handle(GetProjectTasksQuery request, CancellationToken cancellationToken)
    {
        var query = _ppmDbContext.ProjectTasks
            .Where(request.ProjectIdOrKeyFilter);

        if (request.StatusFilter.HasValue)
        {
            query = query.Where(t => t.Status == request.StatusFilter.Value);
        }

        if (request.ParentId.HasValue)
        {
            query = query.Where(t => t.ParentId == request.ParentId.Value);
        }

        return await query
            .ProjectToType<ProjectTaskListDto>()
            .ToListAsync(cancellationToken);
    }
}
