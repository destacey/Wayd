using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Settings;
using Wayd.Work.Application.Iterations.Sprints;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Iterations.Queries;

/// <summary>
/// The sprint each of <paramref name="TeamIds"/> is running now, keyed by team id; a team with no active sprint is
/// missing. <see cref="GetTeamActiveSprintQuery"/> reads one team's in full.
/// </summary>
public sealed record GetTeamsActiveSprintQuery(IReadOnlyCollection<Guid> TeamIds) : IQuery<IReadOnlyDictionary<Guid, NavigationDto>>;

/// <summary>Reads the teams' sprints and schedules once, and works out each sprint's state now.</summary>
public sealed class GetTeamsActiveSprintQueryHandler(
    IWorkDbContext workDbContext,
    IDispatcher dispatcher,
    ISettings<SchedulingSettings> schedulingSettings,
    ISettings<GeneralSettings> generalSettings,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetTeamsActiveSprintQuery, IReadOnlyDictionary<Guid, NavigationDto>>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;
    private readonly ISettings<GeneralSettings> _generalSettings = generalSettings;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, NavigationDto>> Handle(GetTeamsActiveSprintQuery request, CancellationToken cancellationToken)
    {
        var teamIds = request.TeamIds.Distinct().ToList();
        if (teamIds.Count == 0)
            return new Dictionary<Guid, NavigationDto>();

        // Every sprint of each team, since a sprint's state depends on its neighbours.
        var sprints = await _workDbContext.Iterations
            .Where(i => i.TeamId.HasValue && teamIds.Contains(i.TeamId.Value) && i.Type == IterationType.Sprint)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var states = await _dispatcher.BuildIterationStateReader(_schedulingSettings, _generalSettings, sprints, _dateTimeProvider.Now, cancellationToken);

        return sprints
            .Where(s => states.StateOf(s) == IterationState.Active)
            .GroupBy(s => s.TeamId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(s => s.DateRange.Start).ThenBy(s => s.Key).Select(s => new NavigationDto { Id = s.Id, Key = s.Key, Name = s.Name }).First());
    }
}
