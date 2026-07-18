namespace Wayd.Organization.Application.TeamsOfTeams.Queries;

public sealed record TeamOfTeamsExistsQuery : IQuery<bool>
{
    public TeamOfTeamsExistsQuery(Guid teamId)
    {
        TeamId = teamId;
    }

    public TeamOfTeamsExistsQuery(int teamKey)
    {
        TeamKey = teamKey;
    }

    public Guid? TeamId { get; }
    public int? TeamKey { get; }
}

public sealed class TeamOfTeamsExistsQueryHandler(
    IOrganizationDbContext organizationDbContext,
    ILogger<TeamOfTeamsExistsQueryHandler> logger)
    : IQueryHandler<TeamOfTeamsExistsQuery, bool>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly ILogger<TeamOfTeamsExistsQueryHandler> _logger = logger;

    public async Task<bool> Handle(TeamOfTeamsExistsQuery request, CancellationToken cancellationToken)
    {
        var query = _organizationDbContext.TeamOfTeams.AsQueryable();

        if (request.TeamId.HasValue)
        {
            return await query.AnyAsync(e => e.Id == request.TeamId.Value, cancellationToken);
        }
        else if (request.TeamKey.HasValue)
        {
            return await query.AnyAsync(e => e.Key == request.TeamKey.Value, cancellationToken);
        }
        else
        {
            var requestName = request.GetType().Name;
            var exception = new InternalServerException("No team id or local id provided.");

            _logger.LogError(exception, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);
            throw exception;
        }
    }
}
