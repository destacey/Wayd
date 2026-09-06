using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Portfolios.Queries;

/// <summary>
/// Retrieves activity history for a project portfolio, newest first.
/// </summary>
public sealed record GetPortfolioActivitiesQuery : IQuery<Result<PagedResponse<ActivityLogDto>?>>
{
    public GetPortfolioActivitiesQuery(IdOrKey idOrKey, int page = 1, int pageSize = 50)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<ProjectPortfolio>();
        Page = page;
        PageSize = pageSize;
    }

    public Expression<Func<ProjectPortfolio, bool>> IdOrKeyFilter { get; }
    public int Page { get; }
    public int PageSize { get; }
}

public sealed class GetPortfolioActivitiesQueryHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    IActivityLogReader activityLogReader)
    : IQueryHandler<GetPortfolioActivitiesQuery, Result<PagedResponse<ActivityLogDto>?>>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly IActivityLogReader _activityLogReader = activityLogReader;

    public async Task<Result<PagedResponse<ActivityLogDto>?>> Handle(
        GetPortfolioActivitiesQuery request, CancellationToken cancellationToken)
    {
        var portfolioId = await _ppmDbContext.Portfolios
            .AsNoTracking()
            .Where(request.IdOrKeyFilter)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (portfolioId is null)
        {
            return Result.Success<PagedResponse<ActivityLogDto>?>(null);
        }

        var activities = await _activityLogReader.Read(
            portfolioId.Value,
            aggregateType: "ProjectPortfolio",
            page: request.Page,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken);

        return Result.Success<PagedResponse<ActivityLogDto>?>(activities);
    }
}
