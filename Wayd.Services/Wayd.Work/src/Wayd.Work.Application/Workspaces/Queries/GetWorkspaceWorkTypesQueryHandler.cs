using Wayd.Common.Application.Requests.WorkManagement.Interfaces;
using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkTypes.Dtos;

namespace Wayd.Work.Application.Workspaces.Queries;

public sealed class GetWorkspaceWorkTypesQueryHandler(IWorkDbContext workDbContext, ILogger<GetWorkspaceWorkTypesQueryHandler> logger) : IQueryHandler<GetWorkspaceWorkTypesQuery, Result<IReadOnlyList<IWorkTypeDto>>>
{
    private const string AppRequestName = nameof(GetWorkspaceWorkTypesQuery);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<GetWorkspaceWorkTypesQueryHandler> _logger = logger;

    public async Task<Result<IReadOnlyList<IWorkTypeDto>>> Handle(GetWorkspaceWorkTypesQuery request, CancellationToken cancellationToken)
    {
        // TODO: change this to route through the WorkProcess once workflow is wired up
        //var query = _workDbContext.Workspaces
        //    .AsQueryable();

        //if (request.Id.HasValue)
        //{
        //    query = query.Where(e => e.Id == request.Id);
        //}
        //else if (request.Key is not null)
        //{
        //    query = query.Where(e => e.Key == request.Key);
        //}
        //else
        //{
        //    _logger.LogError("{AppRequestName}: No workspace id or key provided. {@Request}", AppRequestName, request);
        //    return Result.Failure<IReadOnlyList<WorkTypeDto>>("No valid workspace id or key provided.");
        //}

        return await _workDbContext.WorkTypes
            .Where(t => t.IsActive)
            .ProjectToType<WorkTypeDto>().ToListAsync(cancellationToken);
    }
}
