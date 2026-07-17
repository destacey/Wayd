namespace Wayd.Organization.Application.Teams.Queries;

public sealed record TeamExistsQuery : IQuery<bool>
{
    public TeamExistsQuery(Guid teamId)
    {
        TeamId = teamId;
    }

    public TeamExistsQuery(int teamKey)
    {
        TeamKey = teamKey;
    }

    public Guid? TeamId { get; }
    public int? TeamKey { get; }
}

public sealed class TeamExistsQueryHandler : IQueryHandler<TeamExistsQuery, bool>
{
    private readonly IOrganizationDbContext _organizationDbContext;
    private readonly ILogger<TeamExistsQueryHandler> _logger;

    public TeamExistsQueryHandler(IOrganizationDbContext organizationDbContext, ILogger<TeamExistsQueryHandler> logger)
    {
        _organizationDbContext = organizationDbContext;
        _logger = logger;
    }

    public async Task<bool> Handle(TeamExistsQuery request, CancellationToken cancellationToken)
    {
        var query = _organizationDbContext.Teams.AsQueryable();

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
