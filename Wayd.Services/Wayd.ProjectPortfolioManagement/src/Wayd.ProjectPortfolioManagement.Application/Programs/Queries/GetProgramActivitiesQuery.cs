using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Programs.Queries;

/// <summary>
/// Retrieves activity history for a program, newest first.
/// </summary>
public sealed record GetProgramActivitiesQuery : IQuery<Result<PagedResponse<ActivityLogDto>?>>
{
    public GetProgramActivitiesQuery(IdOrKey idOrKey, int page = 1, int pageSize = 50)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Program>();
        Page = page;
        PageSize = pageSize;
    }

    public Expression<Func<Program, bool>> IdOrKeyFilter { get; }
    public int Page { get; }
    public int PageSize { get; }
}

public sealed class GetProgramActivitiesQueryHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    IActivityLogReader activityLogReader)
    : IQueryHandler<GetProgramActivitiesQuery, Result<PagedResponse<ActivityLogDto>?>>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly IActivityLogReader _activityLogReader = activityLogReader;

    public async Task<Result<PagedResponse<ActivityLogDto>?>> Handle(
        GetProgramActivitiesQuery request, CancellationToken cancellationToken)
    {
        var programId = await _ppmDbContext.Programs
            .Where(request.IdOrKeyFilter)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (programId is null)
        {
            return Result.Success<PagedResponse<ActivityLogDto>?>(null);
        }

        var activities = await _activityLogReader.Read(
            programId.Value,
            aggregateType: "Program",
            page: request.Page,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken);

        return Result.Success<PagedResponse<ActivityLogDto>?>(activities);
    }
}
