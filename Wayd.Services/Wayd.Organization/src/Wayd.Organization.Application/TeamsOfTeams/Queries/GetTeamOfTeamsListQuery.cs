using Mapster;

namespace Wayd.Organization.Application.TeamsOfTeams.Queries;

public sealed record GetTeamOfTeamsListQuery : IQuery<IReadOnlyList<TeamOfTeamsListDto>>
{
    public GetTeamOfTeamsListQuery(bool includeInactive = false, IEnumerable<Guid>? ids = null)
    {
        IncludeInactive = includeInactive;
        TeamIds = ids?.ToList() ?? [];
    }

    public bool IncludeInactive { get; }
    public List<Guid> TeamIds { get; }
}

public sealed class GetTeamOfTeamsListQueryHandler(
    IOrganizationDbContext organizationDbContext,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetTeamOfTeamsListQuery, IReadOnlyList<TeamOfTeamsListDto>>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<IReadOnlyList<TeamOfTeamsListDto>> Handle(GetTeamOfTeamsListQuery request, CancellationToken cancellationToken)
    {
        var query = _organizationDbContext.TeamOfTeams
            .AsQueryable();

        if (!request.IncludeInactive)
            query = query.Where(e => e.IsActive);

        if (request.TeamIds.Any())
            query = query.Where(e => request.TeamIds.Contains(e.Id));



        var today = _dateTimeProvider.Today;
        var cfg = TeamOfTeamsListDto.CreateTypeAdapterConfig(today);

        return await query
            .ProjectToType<TeamOfTeamsListDto>(cfg)
            .ToListAsync(cancellationToken);
    }
}
