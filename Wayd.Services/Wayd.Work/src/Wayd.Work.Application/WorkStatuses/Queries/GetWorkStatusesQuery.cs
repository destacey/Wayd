using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkStatuses.Dtos;

namespace Wayd.Work.Application.WorkStatuses.Queries;

public sealed record GetWorkStatusesQuery(bool IncludeInactive = false) : IQuery<IReadOnlyList<WorkStatusDto>>;

public sealed class GetWorkStatusesQueryHandler : IQueryHandler<GetWorkStatusesQuery, IReadOnlyList<WorkStatusDto>>
{
    private readonly IWorkDbContext _workDbContext;

    public GetWorkStatusesQueryHandler(IWorkDbContext workDbContext)
    {
        _workDbContext = workDbContext;
    }

    public async Task<IReadOnlyList<WorkStatusDto>> Handle(GetWorkStatusesQuery request, CancellationToken cancellationToken)
    {
        var query = _workDbContext.WorkStatuses.AsQueryable();

        if (!request.IncludeInactive)
            query = query.Where(e => e.IsActive);

        return await query.ProjectToType<WorkStatusDto>().ToListAsync(cancellationToken);
    }
}
