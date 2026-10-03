using System.Linq.Expressions;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Settings;
using Wayd.Work.Application.Iterations.Dtos;
using Wayd.Work.Application.Iterations.Sprints;
using Wayd.Work.Domain.Models;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Iterations.Queries;

public sealed record GetSprintQuery : IQuery<SprintDetailsDto?>
{
    public GetSprintQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Iteration>();
    }

    public Expression<Func<Iteration, bool>> IdOrKeyFilter { get; }
}

public sealed class GetSprintQueryHandler(
    IWorkDbContext workDbContext,
    IDispatcher dispatcher,
    ISettings<SchedulingSettings> schedulingSettings,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetSprintQuery, SprintDetailsDto?>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<SprintDetailsDto?> Handle(GetSprintQuery request, CancellationToken cancellationToken)
    {
        var sprint = await _workDbContext.Iterations
            .Where(request.IdOrKeyFilter)
            .Where(i => i.Type == IterationType.Sprint)
            .ProjectToType<SprintDetailsDto>()
            .FirstOrDefaultAsync(cancellationToken);

        if (sprint?.Team is null)
            return sprint;

        await ResolveLifecycle(sprint, sprint.Team.Id, cancellationToken);

        return sprint;
    }

    /// <summary>
    /// Fills the fields that depend on the team's other sprints and schedules, answered by the same timeline
    /// rules the lifecycle commands enforce, so the page offers only the actions they would accept.
    /// </summary>
    private async Task ResolveLifecycle(SprintDetailsDto sprint, Guid teamId, CancellationToken cancellationToken)
    {
        var timeline = await _workDbContext.LoadTeamSprintTimeline(_dispatcher, _schedulingSettings, teamId, tracked: false, cancellationToken);

        var entity = timeline.Sprints.FirstOrDefault(s => s.Id == sprint.Id);
        if (entity is null)
            return;

        var now = _dateTimeProvider.Now;

        sprint.EffectiveStart = timeline.EffectiveStart(entity);
        sprint.EffectiveEnd = timeline.EffectiveEnd(entity);
        sprint.TimeZone = timeline.ScheduleFor(entity).TimeZone.Id;
        sprint.OverlapsPreviousSprint = timeline.OverlapsPrevious(entity);
        sprint.OverlapsNextSprint = timeline.OverlapsNext(entity);
        var startWindow = timeline.StartWindow(entity, now);
        sprint.CanStart = startWindow.IsSuccess;
        sprint.StartWindow = startWindow.IsSuccess ? InstantWindowDto.From(startWindow.Value, now) : null;

        var completeWindow = timeline.CompleteWindow(entity, now);
        sprint.CanComplete = completeWindow.IsSuccess;
        sprint.CompleteWindow = completeWindow.IsSuccess ? InstantWindowDto.From(completeWindow.Value, now) : null;
        sprint.CanReopen = timeline.CanReopen(entity).IsSuccess;

        if (timeline.OpenSprint is { } open && open.Id != entity.Id)
            sprint.OpenSprint = NavigationDto.Create(open.Id, open.Key, open.Name);

        sprint.CanManageSprint = await _currentPrincipal.CanManageTeamSprints(_dispatcher, teamId, now.InUtc().Date, cancellationToken);
    }
}
