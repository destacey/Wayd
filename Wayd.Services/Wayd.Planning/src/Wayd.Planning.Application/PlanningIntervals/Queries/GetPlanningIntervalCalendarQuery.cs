using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Planning.Application.PlanningIntervals.Dtos;

namespace Wayd.Planning.Application.PlanningIntervals.Queries;

public sealed record GetPlanningIntervalCalendarQuery : IQuery<PlanningIntervalCalendarDto?>
{
    public GetPlanningIntervalCalendarQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<PlanningInterval>();
    }

    public Expression<Func<PlanningInterval, bool>> IdOrKeyFilter { get; }
}

public sealed class GetPlanningIntervalCalendarQueryHandler(IPlanningDbContext planningDbContext, ILogger<GetPlanningIntervalCalendarQueryHandler> logger) : IQueryHandler<GetPlanningIntervalCalendarQuery, PlanningIntervalCalendarDto?>
{
    private const string AppRequestName = nameof(GetPlanningIntervalCalendarQuery);

    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ILogger<GetPlanningIntervalCalendarQueryHandler> _logger = logger;

    public async Task<PlanningIntervalCalendarDto?> Handle(GetPlanningIntervalCalendarQuery request, CancellationToken cancellationToken)
    {
        var planningInterval = await _planningDbContext.PlanningIntervals
            .Include(p => p.Iterations)
            .Where(request.IdOrKeyFilter)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return planningInterval is null
            ? null
            : PlanningIntervalCalendarDto.Create(planningInterval.GetCalendar());
    }
}

public sealed record PlanningIntervalCalendarDto
{
    /// <summary>
    /// The Planning Interval Id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The Planning Interval Key.
    /// </summary>
    public int Key { get; set; }

    /// <summary>
    /// The Planning Interval Name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The Planning Interval Start Date.
    /// </summary>
    public required LocalDate Start { get; set; }

    /// <summary>
    /// The Planning Interval End Date.
    /// </summary>
    public required LocalDate End { get; set; }

    /// <summary>
    /// The Planning Interval Iteration Schedules.
    /// </summary>
    public required List<LocalScheduleDto> IterationSchedules { get; set; }

    public static PlanningIntervalCalendarDto Create(PlanningIntervalCalendar calendar)
    {
        return new PlanningIntervalCalendarDto
        {
            Id = calendar.Id,
            Name = calendar.Name,
            Start = calendar.DateRange.Start,
            End = calendar.DateRange.End,
            IterationSchedules = calendar.IterationSchedules.Select(i => LocalScheduleDto.Create(i)).ToList()
        };
    }
}
