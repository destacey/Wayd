using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Planning.Application.PlanningIntervals.Dtos;

namespace Wayd.Planning.Application.PlanningIntervals.Queries;

public sealed record GetPlanningIntervalQuery : IQuery<PlanningIntervalDetailsDto?>
{
    public GetPlanningIntervalQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<PlanningInterval>();
    }

    public Expression<Func<PlanningInterval, bool>> IdOrKeyFilter { get; }
}

public sealed class GetPlanningIntervalQueryHandler : IQueryHandler<GetPlanningIntervalQuery, PlanningIntervalDetailsDto?>
{
    private readonly IPlanningDbContext _planningDbContext;
    private readonly ILogger<GetPlanningIntervalQueryHandler> _logger;
    private readonly ISystemCalendar _systemCalendar;

    public GetPlanningIntervalQueryHandler(IPlanningDbContext planningDbContext, ILogger<GetPlanningIntervalQueryHandler> logger, ISystemCalendar systemCalendar)
    {
        _planningDbContext = planningDbContext;
        _logger = logger;
        _systemCalendar = systemCalendar;
    }

    public async Task<PlanningIntervalDetailsDto?> Handle(GetPlanningIntervalQuery request, CancellationToken cancellationToken)
    {
        var today = await _systemCalendar.Today(cancellationToken);
        return await _planningDbContext.PlanningIntervals
            .Include(p => p.Objectives)
            .Where(request.IdOrKeyFilter)
            .Select(p => PlanningIntervalDetailsDto.Create(p, today))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
