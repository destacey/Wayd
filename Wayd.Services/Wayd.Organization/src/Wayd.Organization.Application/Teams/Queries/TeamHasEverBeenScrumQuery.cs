using Wayd.Organization.Domain.Enums;

namespace Wayd.Organization.Application.Teams.Queries;

/// <summary>
/// Checks if a team has ever used the Scrum methodology.
/// Used by the UI to determine whether to show the Sprints tab.
/// </summary>
public sealed record TeamHasEverBeenScrumQuery(Guid TeamId) : IQuery<bool?>;

public sealed class TeamHasEverBeenScrumQueryHandler(IOrganizationDbContext organizationDbContext)
    : IQueryHandler<TeamHasEverBeenScrumQuery, bool?>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;

    public async Task<bool?> Handle(TeamHasEverBeenScrumQuery request, CancellationToken cancellationToken)
    {
        var teamExists = await _organizationDbContext.Teams
            .AnyAsync(t => t.Id == request.TeamId, cancellationToken);
        if (!teamExists)
            return null;

        // Query through Team aggregate
        return await _organizationDbContext.Teams
            .Where(t => t.Id == request.TeamId)
            .SelectMany(t => t.OperatingModels)
            .AnyAsync(m => m.Methodology == Methodology.Scrum, cancellationToken);
    }
}
