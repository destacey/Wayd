using Wayd.Planning.Application.Risks.Dtos;
using Wayd.Planning.Domain.Enums;

namespace Wayd.Planning.Application.Risks.Queries;

public sealed record GetRisksQuery : IQuery<IReadOnlyList<RiskListDto>>
{
    public GetRisksQuery(bool includeClosed)
    {
        IncludeClosed = includeClosed;
    }

    public GetRisksQuery(Guid teamId, bool includeClosed)
    {
        TeamIds = [teamId];
        IncludeClosed = includeClosed;
    }

    public GetRisksQuery(IEnumerable<Guid> teamIds, bool includeClosed)
    {
        TeamIds = teamIds.ToList();
        IncludeClosed = includeClosed;
    }

    public List<Guid> TeamIds { get; private set; } = [];
    public bool IncludeClosed { get; private set; }
}

public sealed class GetRisksQueryHandler : IQueryHandler<GetRisksQuery, IReadOnlyList<RiskListDto>>
{
    private readonly IPlanningDbContext _planningDbContext;

    public GetRisksQueryHandler(IPlanningDbContext planningDbContext)
    {
        _planningDbContext = planningDbContext;
    }

    public async Task<IReadOnlyList<RiskListDto>> Handle(GetRisksQuery request, CancellationToken cancellationToken)
    {
        var query = _planningDbContext.Risks
            .Include(r => r.Team)
            .Include(r => r.Assignee)
            .AsQueryable();

        if (request.TeamIds.Any())
        {
            query = query.Where(r => r.TeamId.HasValue && request.TeamIds.Contains(r.TeamId.Value));
        }

        if (!request.IncludeClosed)
        {
            query = query.Where(r => r.Status != RiskStatus.Closed);
        }

        var risks = await query
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return risks.Adapt<IReadOnlyList<RiskListDto>>();
    }
}
