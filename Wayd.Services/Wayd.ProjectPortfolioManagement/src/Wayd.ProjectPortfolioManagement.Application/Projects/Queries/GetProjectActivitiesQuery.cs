using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.ProjectPortfolioManagement.Application.Projects.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Queries;

/// <summary>
/// Retrieves activity history for a project, newest first.
/// </summary>
public sealed record GetProjectActivitiesQuery : IQuery<Result<PagedResponse<ActivityLogDto>?>>
{
    public GetProjectActivitiesQuery(ProjectIdOrKey idOrKey, int page = 1, int pageSize = 50)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Project>();
        Page = page;
        PageSize = pageSize;
    }

    public Expression<Func<Project, bool>> IdOrKeyFilter { get; }
    public int Page { get; }
    public int PageSize { get; }
}

public sealed class GetProjectActivitiesQueryHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    IActivityLogReader activityLogReader)
    : IQueryHandler<GetProjectActivitiesQuery, Result<PagedResponse<ActivityLogDto>?>>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly IActivityLogReader _activityLogReader = activityLogReader;

    public async Task<Result<PagedResponse<ActivityLogDto>?>> Handle(
        GetProjectActivitiesQuery request, CancellationToken cancellationToken)
    {
        var projectId = await _ppmDbContext.Projects
            .AsNoTracking()
            .Where(request.IdOrKeyFilter)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (projectId is null)
        {
            return Result.Success<PagedResponse<ActivityLogDto>?>(null);
        }

        var activities = await _activityLogReader.Read(
            projectId.Value,
            aggregateType: "Project",
            page: request.Page,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken);

        return Result.Success<PagedResponse<ActivityLogDto>?>(activities);
    }
}
