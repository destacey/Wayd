using Wayd.Common.Application.Search;
using Wayd.Common.Application.Search.Dtos;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.Organization;

namespace Wayd.Organization.Application.Search;

public sealed class SearchOrganizationForGlobalSearchQueryHandler(IOrganizationDbContext organizationDbContext)
    : IQueryHandler<SearchOrganizationForGlobalSearchQuery, ServiceSearchResponse>
{
    public async Task<ServiceSearchResponse> Handle(SearchOrganizationForGlobalSearchQuery request, CancellationToken cancellationToken)
    {
        var term = request.Request.SearchTerm;
        var max = request.Request.MaxResultsPerCategory;
        var categories = new List<GlobalSearchCategoryDto>();

        // Teams (includes Teams and TeamsOfTeams via BaseTeams)
        var teamQuery = organizationDbContext.BaseTeams
            .Where(t => !t.IsDeleted && t.IsActive)
            .Where(t => t.Name.Contains(term) || ((string)t.Code).Contains(term));

        var teamCount = await teamQuery.CountAsync(cancellationToken);
        var teams = await teamQuery
            .OrderBy(t => t.Name)
            .Select(t => new GlobalSearchResultItemDto
            {
                Title = t.Name,
                Subtitle = null,
                Key = ((string)t.Code),
                EntityType = t.Type == TeamType.TeamOfTeams ? nameof(TeamOfTeams) : nameof(Team),
                AuxKey = t.Key.ToString()
            })
            .Take(max)
            .ToListAsync(cancellationToken);

        categories.Add(new GlobalSearchCategoryDto
        {
            Name = "Teams",
            Slug = "teams",
            Items = teams,
            TotalCount = teamCount
        });

        // Employees
        var employeeQuery = organizationDbContext.Employees
            .Where(e => !e.IsDeleted && e.IsActive)
            .Where(e => e.Name.FirstName.Contains(term)
                || e.Name.LastName.Contains(term));

        var employeeCount = await employeeQuery.CountAsync(cancellationToken);
        var employees = await employeeQuery
            .OrderBy(e => e.Name.LastName)
            .ThenBy(e => e.Name.FirstName)
            .Select(e => new GlobalSearchResultItemDto
            {
                Title = e.Name.FirstName + " " + e.Name.LastName,
                Subtitle = null,
                Key = e.Key.ToString(),
                EntityType = nameof(Employee)
            })
            .Take(max)
            .ToListAsync(cancellationToken);

        categories.Add(new GlobalSearchCategoryDto
        {
            Name = "Employees",
            Slug = "employees",
            Items = employees,
            TotalCount = employeeCount
        });

        return new ServiceSearchResponse { Categories = categories };
    }
}
