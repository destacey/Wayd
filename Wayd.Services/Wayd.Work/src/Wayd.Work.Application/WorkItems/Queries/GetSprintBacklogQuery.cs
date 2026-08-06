using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItems.Dtos;

namespace Wayd.Work.Application.WorkItems.Queries;

public sealed record GetSprintBacklogQuery : IQuery<List<SprintBacklogItemDto>?>
{
    public GetSprintBacklogQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<WorkIteration>();
    }

    public Expression<Func<WorkIteration, bool>> IdOrKeyFilter { get; }
}

public sealed class GetSprintBacklogQueryHandler(IWorkDbContext workDbContext, ILogger<GetSprintBacklogQueryHandler> logger) : IQueryHandler<GetSprintBacklogQuery, List<SprintBacklogItemDto>?>
{
    private const string AppRequestName = nameof(GetSprintBacklogQuery);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<GetSprintBacklogQueryHandler> _logger = logger;

    public async Task<List<SprintBacklogItemDto>?> Handle(GetSprintBacklogQuery request, CancellationToken cancellationToken)
    {
        // Cast to Guid? or the HasValue check below never fires: FirstOrDefaultAsync over a non-nullable
        // Guid returns Guid.Empty on a miss, making an unknown sprint a 200 with an empty backlog, not a 404.
        var sprintId = await _workDbContext.WorkIterations
            .Where(request.IdOrKeyFilter)
            .Where(i => i.Type == IterationType.Sprint)
            .Select(i => (Guid?)i.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (!sprintId.HasValue)
            return null;

        var workItems = await _workDbContext.WorkItems
            .Where(w => w.IterationId == sprintId.Value)
            .ProjectToType<SprintBacklogItemDto>()
            .ToListAsync(cancellationToken);

        if (workItems.Count == 0)
        {
            return [];
        }

        var rank = 1;
        var backlog = workItems
            .OrderBy(w => w.StackRank)
            .ThenBy(w => w.Created);
        foreach (var workItem in backlog)
        {
            workItem.Rank = rank++;
        }

        return [.. backlog];
    }
}