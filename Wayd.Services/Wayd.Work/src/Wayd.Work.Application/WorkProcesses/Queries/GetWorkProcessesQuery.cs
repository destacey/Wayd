using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkProcesses.Dtos;

namespace Wayd.Work.Application.WorkProcesses.Queries;

public sealed record GetWorkProcessesQuery(bool IncludeInactive = false) : IQuery<IReadOnlyList<WorkProcessListDto>>;

public sealed class GetWorkProcessesQueryHandler(IWorkDbContext workDbContext, ILogger<GetWorkProcessesQueryHandler> logger) : IQueryHandler<GetWorkProcessesQuery, IReadOnlyList<WorkProcessListDto>>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<GetWorkProcessesQueryHandler> _logger = logger;

    public async Task<IReadOnlyList<WorkProcessListDto>> Handle(GetWorkProcessesQuery request, CancellationToken cancellationToken)
    {
        var query = _workDbContext.WorkProcesses.AsQueryable();

        if (!request.IncludeInactive)
            query = query.Where(e => e.IsActive);

        return await query.ProjectToType<WorkProcessListDto>().ToListAsync(cancellationToken);
    }
}
