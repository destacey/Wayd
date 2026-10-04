using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Settings;
using Wayd.Work.Application.Iterations.Dtos;
using Wayd.Work.Application.Iterations.Sprints;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Iterations.Queries;

public sealed record GetSprintsQuery(Guid? TeamId = null) : IQuery<List<SprintListDto>>;

public sealed class GetSprintsQueryHandler(
    IWorkDbContext workDbContext,
    IDispatcher dispatcher,
    ISettings<SchedulingSettings> schedulingSettings,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetSprintsQuery, List<SprintListDto>>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<List<SprintListDto>> Handle(GetSprintsQuery request, CancellationToken cancellationToken)
    {
        var query = _workDbContext.Iterations
            .Where(i => i.Type == IterationType.Sprint && i.TeamId != null)
            .AsQueryable();

        if (request.TeamId.HasValue)
        {
            query = query.Where(i => i.TeamId == request.TeamId.Value);
        }

        // Every sprint of each team in the result, which is what each team's timeline needs.
        var sprints = await query
            .Include(i => i.Team)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var states = await _dispatcher.BuildIterationStateReader(_schedulingSettings, sprints, _dateTimeProvider.Now, cancellationToken);

        return [.. sprints.Select(sprint =>
        {
            var dto = sprint.Adapt<SprintListDto>();
            dto.State = SimpleNavigationDto.FromEnum(states.StateOf(sprint));
            return dto;
        })];
    }
}
