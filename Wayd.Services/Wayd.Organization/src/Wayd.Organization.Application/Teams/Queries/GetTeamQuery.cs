using Mapster;

namespace Wayd.Organization.Application.Teams.Queries;

public sealed record GetTeamQuery : IQuery<TeamDetailsDto?>
{
    public GetTeamQuery(Guid teamId)
    {
        TeamId = teamId;
    }

    public GetTeamQuery(int teamKey)
    {
        TeamKey = teamKey;
    }

    public Guid? TeamId { get; }
    public int? TeamKey { get; }
}

public sealed class GetTeamQueryHandler(
    IOrganizationDbContext organizationDbContext,
    ILogger<GetTeamQueryHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetTeamQuery, TeamDetailsDto?>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly ILogger<GetTeamQueryHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<TeamDetailsDto?> Handle(GetTeamQuery request, CancellationToken cancellationToken)
    {
        var query = _organizationDbContext.Teams
            .AsQueryable();

        if (request.TeamId.HasValue)
        {
            query = query.Where(e => e.Id == request.TeamId.Value);
        }
        else if (request.TeamKey.HasValue)
        {
            query = query.Where(e => e.Key == request.TeamKey.Value);
        }
        else
        {
            var requestName = request.GetType().Name;
            var exception = new InternalServerException("No team id or local id provided.");

            _logger.LogError(exception, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);
            throw exception;
        }

        var today = _dateTimeProvider.Today;
        var cfg = TeamDetailsDto.CreateTypeAdapterConfig(today);

        return await query
            .ProjectToType<TeamDetailsDto>(cfg)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
