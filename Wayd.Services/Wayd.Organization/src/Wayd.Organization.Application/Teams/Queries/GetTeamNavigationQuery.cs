using Mapster;
using Wayd.Organization.Application.Models;

namespace Wayd.Organization.Application.Teams.Queries;

public sealed record GetTeamNavigationQuery : IQuery<TeamNavigationDto?>
{
    public GetTeamNavigationQuery(Guid teamId)
    {
        TeamId = teamId;
    }

    public GetTeamNavigationQuery(int teamKey)
    {
        TeamKey = teamKey;
    }

    public Guid? TeamId { get; }
    public int? TeamKey { get; }
}

public sealed class GetTeamNavigationQueryHandler : IQueryHandler<GetTeamNavigationQuery, TeamNavigationDto?>
{
    private readonly IOrganizationDbContext _organizationDbContext;
    private readonly ILogger<GetTeamNavigationQueryHandler> _logger;

    public GetTeamNavigationQueryHandler(IOrganizationDbContext organizationDbContext, ILogger<GetTeamNavigationQueryHandler> logger)
    {
        _organizationDbContext = organizationDbContext;
        _logger = logger;
    }

    public async Task<TeamNavigationDto?> Handle(GetTeamNavigationQuery request, CancellationToken cancellationToken)
    {
        var query = _organizationDbContext.Teams.AsQueryable();

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

        return await query
            .ProjectToType<TeamNavigationDto>()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
