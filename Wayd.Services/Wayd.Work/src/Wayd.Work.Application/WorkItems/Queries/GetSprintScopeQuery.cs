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
/// Gets what a sprint committed to and what became of it, from work item history. Null when there is no such
/// sprint, or it has no planned dates to measure between.
/// </summary>
public sealed record GetSprintScopeQuery : IQuery<SprintScopeDto?>
{
    public GetSprintScopeQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Iteration>();
    }

    public Expression<Func<Iteration, bool>> IdOrKeyFilter { get; }
}

public sealed class GetSprintScopeQueryHandler(
    IWorkDbContext workDbContext,
    IDispatcher dispatcher,
    ISettings<SchedulingSettings> schedulingSettings,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetSprintScopeQuery, SprintScopeDto?>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<SprintScopeDto?> Handle(GetSprintScopeQuery request, CancellationToken cancellationToken)
    {
        var history = await _workDbContext.LoadSprintScopeHistory(_dispatcher, _schedulingSettings, request.IdOrKeyFilter, cancellationToken);
        if (history is null)
            return null;

        var (sprint, window, sizingMethod, periods, historyIncomplete) = history;
        var report = SprintScopeReport.Build(window, sizingMethod, periods, _dateTimeProvider.Now);

        var workItemIds = report.Items.Select(i => i.WorkItemId).ToList();
        var workItems = await _workDbContext.WorkItems
            .Where(w => workItemIds.Contains(w.Id))
            .ProjectToType<SprintBacklogItemDto>()
            .ToDictionaryAsync(w => w.Id, cancellationToken);

        // An item deleted between the two reads is left out of the totals too, so they add up to the rows.
        var items = report.Items.Where(i => workItems.ContainsKey(i.WorkItemId)).ToList();

        return new SprintScopeDto
        {
            SprintId = sprint.Id,
            SizingMethod = sizingMethod,
            EffectiveStart = window.Start,
            StartIsActual = window.StartIsActual,
            EffectiveEnd = window.End,
            EndIsActual = window.EndIsActual,
            LastDay = window.LastDay,
            TimeZone = window.TimeZone.Id,
            HasTeam = sprint.TeamId is not null,
            HistoryIncomplete = historyIncomplete,
            Totals = SprintScopeTotalsDto.From(SprintScopeTotals.Of(items)),
            Items = [.. items
                .Select(i => new SprintScopeItemDto
                {
                    WorkItem = workItems[i.WorkItemId],
                    Entry = i.Entry,
                    Outcome = i.Outcome,
                    AddedAt = i.AddedAt,
                    RemovedAt = i.RemovedAt,
                    EntryEstimate = i.EntryEstimate,
                    OutcomeEstimate = i.OutcomeEstimate,
                })
                .OrderBy(i => i.WorkItem.StackRank)
                .ThenBy(i => i.WorkItem.Created)],
        };
    }
}
