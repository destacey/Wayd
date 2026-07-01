using Mapster;

namespace Wayd.Organization.Application.Teams.Queries;

public sealed record GetTeamsQuery : IQuery<IReadOnlyList<TeamListDto>>
{
    public GetTeamsQuery(bool includeInactive = false, IEnumerable<Guid>? ids = null)
    {
        IncludeInactive = includeInactive;
        TeamIds = ids?.ToList() ?? [];
    }

    public bool IncludeInactive { get; }
    public List<Guid> TeamIds { get; }
}

internal sealed class GetTeamsQueryHandler(
    IOrganizationDbContext organizationDbContext,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetTeamsQuery, IReadOnlyList<TeamListDto>>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<IReadOnlyList<TeamListDto>> Handle(GetTeamsQuery request, CancellationToken cancellationToken)
    {
        var query = _organizationDbContext.Teams
            .AsQueryable();

        if (!request.IncludeInactive)
            query = query.Where(e => e.IsActive);

        if (request.TeamIds.Any())
            query = query.Where(e => request.TeamIds.Contains(e.Id));


        var today = _dateTimeProvider.Today;
        var cfg = TeamListDto.CreateTypeAdapterConfig(today);

        return await query
            .ProjectToType<TeamListDto>(cfg)
            .ToListAsync(cancellationToken);
    }
}
