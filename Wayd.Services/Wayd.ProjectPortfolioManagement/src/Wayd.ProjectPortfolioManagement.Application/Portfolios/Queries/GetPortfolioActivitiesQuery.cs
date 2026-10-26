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
public sealed record GetPortfolioActivitiesQuery : IQuery<Result<PagedResponse<ActivityLogDto>?>>, IPagedQuery
{
    public GetPortfolioActivitiesQuery(IdOrKey idOrKey, PageRequest paging)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<ProjectPortfolio>();
        Paging = paging;
    }

    public Expression<Func<ProjectPortfolio, bool>> IdOrKeyFilter { get; }
    public PageRequest Paging { get; }
}

/// <summary>Bounds the page a caller may ask for.</summary>
public sealed class GetPortfolioActivitiesQueryValidator : PagedQueryValidator<GetPortfolioActivitiesQuery>;

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
            paging: request.Paging,
            cancellationToken: cancellationToken);

        return Result.Success<PagedResponse<ActivityLogDto>?>(activities);
    }
}
