using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Settings;
using Wayd.Work.Application.Iterations.Dtos;
using Wayd.Work.Application.Iterations.Sprints;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Iterations.Queries;

public sealed record GetTeamActiveSprintQuery(Guid TeamId) : IQuery<SprintDetailsDto?>;

public sealed class GetTeamActiveSprintQueryValidator : CustomValidator<GetTeamActiveSprintQuery>
{
    public GetTeamActiveSprintQueryValidator()
    {
        RuleFor(q => q.TeamId)
            .NotEmpty();
    }
}

public sealed class GetTeamActiveSprintQueryHandler(
    IWorkDbContext workDbContext,
    IDispatcher dispatcher,
    ISettings<SchedulingSettings> schedulingSettings,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetTeamActiveSprintQuery, SprintDetailsDto?>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<SprintDetailsDto?> Handle(GetTeamActiveSprintQuery request, CancellationToken cancellationToken)
    {
        var timeline = await _workDbContext.LoadTeamSprintTimeline(_dispatcher, _schedulingSettings, request.TeamId, tracked: false, cancellationToken);

        var now = _dateTimeProvider.Now;
        var active = timeline.Sprints.FirstOrDefault(s => timeline.StateAt(s, now) == IterationState.Active);
        if (active is null)
            return null;

        var details = active.Adapt<SprintDetailsDto>();
        await details.Resolve(active, timeline, _currentPrincipal, _dispatcher, now, cancellationToken);

        return details;
    }
}
