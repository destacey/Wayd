using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkTypeLevels.Queries;

public sealed record GetWorkTypeLevelQuery(int Id) : IQuery<WorkTypeLevelDto?>;

public sealed class GetWorkTypeLevelQueryHandler : IQueryHandler<GetWorkTypeLevelQuery, WorkTypeLevelDto?>
{
    private readonly IWorkDbContext _workDbContext;
    private readonly ILogger<GetWorkTypeLevelQueryHandler> _logger;

    public GetWorkTypeLevelQueryHandler(IWorkDbContext workDbContext, ILogger<GetWorkTypeLevelQueryHandler> logger)
    {
        _workDbContext = workDbContext;
        _logger = logger;
    }

    public async Task<WorkTypeLevelDto?> Handle(GetWorkTypeLevelQuery request, CancellationToken cancellationToken)
    {
        return await _workDbContext.WorkTypeHierarchies
            .SelectMany(s => s.Levels.Where(c => c.Id == request.Id))
            .ProjectToType<WorkTypeLevelDto>()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
