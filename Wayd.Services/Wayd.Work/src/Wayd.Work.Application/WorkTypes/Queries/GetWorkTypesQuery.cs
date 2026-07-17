using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkTypes.Dtos;

namespace Wayd.Work.Application.WorkTypes.Queries;

public sealed record GetWorkTypesQuery : IQuery<IReadOnlyList<WorkTypeDto>>
{
    public GetWorkTypesQuery(bool includeInactive = false)
    {
        IncludeInactive = includeInactive;
    }

    public bool IncludeInactive { get; }
}

public sealed class GetWorkTypesQueryHandler : IQueryHandler<GetWorkTypesQuery, IReadOnlyList<WorkTypeDto>>
{
    private readonly IWorkDbContext _workDbContext;

    public GetWorkTypesQueryHandler(IWorkDbContext workDbContext)
    {
        _workDbContext = workDbContext;
    }

    public async Task<IReadOnlyList<WorkTypeDto>> Handle(GetWorkTypesQuery request, CancellationToken cancellationToken)
    {
        var query = _workDbContext.WorkTypes.AsQueryable();

        if (!request.IncludeInactive)
            query = query.Where(e => e.IsActive);

        return await query.ProjectToType<WorkTypeDto>().ToListAsync(cancellationToken);
    }
}
