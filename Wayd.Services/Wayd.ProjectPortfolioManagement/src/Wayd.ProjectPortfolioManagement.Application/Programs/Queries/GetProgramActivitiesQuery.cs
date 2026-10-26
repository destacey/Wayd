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
public sealed record GetProgramActivitiesQuery : IQuery<Result<PagedResponse<ActivityLogDto>?>>, IPagedQuery
{
    public GetProgramActivitiesQuery(IdOrKey idOrKey, PageRequest paging)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Program>();
        Paging = paging;
    }

    public Expression<Func<Program, bool>> IdOrKeyFilter { get; }
    public PageRequest Paging { get; }
}

/// <summary>Bounds the page a caller may ask for.</summary>
public sealed class GetProgramActivitiesQueryValidator : PagedQueryValidator<GetProgramActivitiesQuery>;

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
            paging: request.Paging,
            cancellationToken: cancellationToken);

        return Result.Success<PagedResponse<ActivityLogDto>?>(activities);
    }
}
