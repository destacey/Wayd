using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Common.Domain.Settings;
using Wayd.Work.Application.Iterations.Sprints;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Iterations.Queries;

public sealed class GetIterationStatesQueryHandler(
    IWorkDbContext workDbContext,
    IDispatcher dispatcher,
    ISettings<SchedulingSettings> schedulingSettings,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetIterationStatesQuery, IReadOnlyDictionary<Guid, IterationStateDto>>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<IReadOnlyDictionary<Guid, IterationStateDto>> Handle(GetIterationStatesQuery request, CancellationToken cancellationToken)
    {
        if (request.Ids.Count == 0)
            return new Dictionary<Guid, IterationStateDto>();

        var iterations = await _workDbContext.Iterations
            .Where(i => request.Ids.Contains(i.Id))
            .Select(i => new { i.Id, i.Type, i.TeamId, i.DateRange.Start, i.DateRange.End })
            .ToListAsync(cancellationToken);

        var states = await _workDbContext.LoadIterationStateReader(
            _dispatcher,
            _schedulingSettings,
            iterations.Where(i => i.Type == IterationType.Sprint && i.TeamId.HasValue).Select(i => i.TeamId!.Value),
            _dateTimeProvider.Now,
            cancellationToken);

        return iterations.ToDictionary(i => i.Id, i =>
        {
            var active = states.ActivePeriodOf(i.Id);
            return new IterationStateDto(
                states.StateOf(i.Id, new IterationDateRange(i.Start, i.End)),
                active?.From,
                active?.Until,
                active?.TimeZone.Id);
        });
    }
}
