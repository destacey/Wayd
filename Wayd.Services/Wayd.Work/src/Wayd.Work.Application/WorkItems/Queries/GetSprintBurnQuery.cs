using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Settings;
using Wayd.Work.Application.Iterations.Sprints;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Domain.Models.SprintScope;

namespace Wayd.Work.Application.WorkItems.Queries;

/// <summary>
/// Gets a sprint's burn-up and burn-down from work item history. Null when there is no such sprint, or it has
/// no planned dates to measure between.
/// </summary>
public sealed record GetSprintBurnQuery : IQuery<SprintBurnDto?>
{
    public GetSprintBurnQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Iteration>();
    }

    public Expression<Func<Iteration, bool>> IdOrKeyFilter { get; }
}

public sealed class GetSprintBurnQueryHandler(
    IWorkDbContext workDbContext,
    IDispatcher dispatcher,
    ISettings<SchedulingSettings> schedulingSettings,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetSprintBurnQuery, SprintBurnDto?>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<SprintBurnDto?> Handle(GetSprintBurnQuery request, CancellationToken cancellationToken)
    {
        var history = await _workDbContext.LoadSprintScopeHistory(_dispatcher, _schedulingSettings, request.IdOrKeyFilter, cancellationToken);
        if (history is null)
            return null;

        var burn = SprintBurn.Build(history.Window, history.SizingMethod, history.Periods, _dateTimeProvider.Now);

        return new SprintBurnDto
        {
            SprintId = history.Sprint.Id,
            SizingMethod = history.SizingMethod,
            EffectiveStart = history.Window.Start,
            EffectiveEnd = history.Window.End,
            TimeZone = history.Window.TimeZone.Id,
            HistoryIncomplete = history.HistoryIncomplete,
            Committed = SprintScopeMeasureDto.From(burn.Committed),
            Points = [.. burn.Points.Select(SprintBurnPointDto.From)],
        };
    }
}
