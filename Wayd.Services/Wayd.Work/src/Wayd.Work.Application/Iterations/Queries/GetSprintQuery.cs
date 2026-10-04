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
            .Include(i => i.Team)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (sprint is null)
            return null;

        var details = sprint.Adapt<SprintDetailsDto>();
        var now = _dateTimeProvider.Now;

        if (sprint.TeamId is not { } teamId)
        {
            var states = await _dispatcher.BuildIterationStateReader(_schedulingSettings, [], now, cancellationToken);
            details.State = SimpleNavigationDto.FromEnum(states.StateOf(sprint));
            return details;
        }

        var timeline = await _workDbContext.LoadTeamSprintTimeline(_dispatcher, _schedulingSettings, teamId, tracked: false, cancellationToken);
        await details.Resolve(sprint, timeline, _currentPrincipal, _dispatcher, now, cancellationToken);

        return details;
    }
}
