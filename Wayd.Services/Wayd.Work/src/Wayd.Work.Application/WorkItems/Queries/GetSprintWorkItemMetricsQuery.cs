using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItems.Dtos;

namespace Wayd.Work.Application.WorkItems.Queries;

/// <summary>
/// Query to get work item metrics for a single sprint.
/// </summary>
public sealed record GetSprintWorkItemMetricsQuery : IQuery<SprintWorkItemMetricsDto?>
{
    public GetSprintWorkItemMetricsQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<WorkIteration>();
    }

    public Expression<Func<WorkIteration, bool>> IdOrKeyFilter { get; }
}

internal sealed class GetSprintWorkItemMetricsQueryHandler(
    IWorkDbContext workDbContext,
    ILogger<GetSprintWorkItemMetricsQueryHandler> logger)
    : IQueryHandler<GetSprintWorkItemMetricsQuery, SprintWorkItemMetricsDto?>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<GetSprintWorkItemMetricsQueryHandler> _logger = logger;

    public async Task<SprintWorkItemMetricsDto?> Handle(
        GetSprintWorkItemMetricsQuery request,
        CancellationToken cancellationToken)
    {
        Guid? sprintId = await _workDbContext.WorkIterations
            .Where(request.IdOrKeyFilter)
            .Where(i => i.Type == IterationType.Sprint)
            .Select(i => i.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!sprintId.HasValue)
            return null;

        var workItems = await _workDbContext.WorkItems
            .Where(w => w.IterationId == sprintId.Value)
            .ToListAsync(cancellationToken);

        return SprintWorkItemMetricsDto.FromWorkItems(sprintId.Value, workItems);
    }
}
