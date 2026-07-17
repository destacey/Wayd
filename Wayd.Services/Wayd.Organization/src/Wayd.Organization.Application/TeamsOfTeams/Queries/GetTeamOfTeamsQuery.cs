using Mapster;

namespace Wayd.Organization.Application.TeamsOfTeams.Queries;

public sealed record GetTeamOfTeamsQuery : IQuery<TeamOfTeamsDetailsDto?>
{
    public GetTeamOfTeamsQuery(Guid teamId)
    {
        TeamOfTeamsId = teamId;
    }

    public GetTeamOfTeamsQuery(int teamKey)
    {
        TeamOfTeamsKey = teamKey;
    }

    public Guid? TeamOfTeamsId { get; }
    public int? TeamOfTeamsKey { get; }
}

public sealed class GetTeamOfTeamsQueryHandler(
    IOrganizationDbContext organizationDbContext,
    ILogger<GetTeamOfTeamsQueryHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetTeamOfTeamsQuery, TeamOfTeamsDetailsDto?>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly ILogger<GetTeamOfTeamsQueryHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<TeamOfTeamsDetailsDto?> Handle(GetTeamOfTeamsQuery request, CancellationToken cancellationToken)
    {
        var query = _organizationDbContext.TeamOfTeams
            .AsQueryable();

        if (request.TeamOfTeamsId.HasValue)
        {
            query = query.Where(e => e.Id == request.TeamOfTeamsId.Value);
        }
        else if (request.TeamOfTeamsKey.HasValue)
        {
            query = query.Where(e => e.Key == request.TeamOfTeamsKey.Value);
        }
        else
        {
            var requestName = request.GetType().Name;
            var exception = new InternalServerException("No id or local id provided.");

            _logger.LogError(exception, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);
            throw exception;
        }

        var today = _dateTimeProvider.Today;
        var cfg = TeamOfTeamsDetailsDto.CreateTypeAdapterConfig(today);

        return await query
            .ProjectToType<TeamOfTeamsDetailsDto>(cfg)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
