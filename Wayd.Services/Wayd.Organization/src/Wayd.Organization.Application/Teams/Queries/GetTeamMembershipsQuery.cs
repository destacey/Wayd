using Wayd.Organization.Application.Models;

namespace Wayd.Organization.Application.Teams.Queries;

public sealed record GetTeamMembershipsQuery : IQuery<IReadOnlyList<TeamMembershipDto>>
{
    public GetTeamMembershipsQuery(Guid teamId)
    {
        TeamId = teamId;
    }

    public GetTeamMembershipsQuery(int teamKey)
    {
        TeamKey = teamKey;
    }

    public Guid? TeamId { get; }
    public int? TeamKey { get; }
}

public sealed class GetTeamMembershipsQueryHandler : IQueryHandler<GetTeamMembershipsQuery, IReadOnlyList<TeamMembershipDto>>
{
    private readonly IOrganizationDbContext _organizationDbContext;
    private readonly ILogger<GetTeamMembershipsQueryHandler> _logger;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetTeamMembershipsQueryHandler(IOrganizationDbContext organizationDbContext, ILogger<GetTeamMembershipsQueryHandler> logger, IDateTimeProvider dateTimeProvider)
    {
        _organizationDbContext = organizationDbContext;
        _logger = logger;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<IReadOnlyList<TeamMembershipDto>> Handle(GetTeamMembershipsQuery request, CancellationToken cancellationToken)
    {
        var query = _organizationDbContext.Teams
            .Include(t => t.ParentMemberships)
                .ThenInclude(m => m.Target)
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

        var team = await query
            .SingleAsync(cancellationToken);

        return team.ParentMemberships.Select(m => TeamMembershipDto.Create(m, _dateTimeProvider)).ToList();
    }
}
