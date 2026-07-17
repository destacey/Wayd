using Ardalis.GuardClauses;
using Wayd.Common.Application.Requests.Goals.Queries;
using Wayd.Common.Application.Search;
using Wayd.Common.Application.Search.Dtos;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Domain.Models.Iterations;
using Wayd.Planning.Domain.Models.Roadmaps;

namespace Wayd.Planning.Application.Search;

internal sealed class SearchPlanningForGlobalSearchQueryHandler(IPlanningDbContext planningDbContext, IDispatcher dispatcher, IDateTimeProvider dateTimeProvider, ICurrentUser currentUser)
    : IQueryHandler<SearchPlanningForGlobalSearchQuery, ServiceSearchResponse>
{
    public async Task<ServiceSearchResponse> Handle(SearchPlanningForGlobalSearchQuery request, CancellationToken cancellationToken)
    {
        var term = request.Request.SearchTerm;
        var max = request.Request.MaxResultsPerCategory;
        var categories = new List<GlobalSearchCategoryDto>();

        // Planning Intervals
        var piQuery = planningDbContext.PlanningIntervals
            .Where(pi => pi.Name.Contains(term));

        var piCount = await piQuery.CountAsync(cancellationToken);
        var pis = await piQuery
            .OrderBy(pi => pi.Name)
            .Select(pi => new GlobalSearchResultItemDto
            {
                Title = pi.Name,
                Subtitle = null,
                Key = pi.Key.ToString(),
                EntityType = nameof(PlanningInterval)
            })
            .Take(max)
            .ToListAsync(cancellationToken);

        categories.Add(new GlobalSearchCategoryDto
        {
            Name = "Planning Intervals",
            Slug = "planning-intervals",
            Items = pis,
            TotalCount = piCount
        });

        // PI Iterations
        var iterationQuery = planningDbContext.PlanningIntervals
            .SelectMany(pi => pi.Iterations, (pi, iter) => new { pi, iter })
            .Where(x => x.iter.Name.Contains(term));

        var iterationCount = await iterationQuery.CountAsync(cancellationToken);
        var iterations = await iterationQuery
            .OrderBy(x => x.pi.Name)
            .ThenBy(x => x.iter.Name)
            .Select(x => new GlobalSearchResultItemDto
            {
                Title = x.iter.Name,
                Subtitle = x.pi.Name,
                Key = x.iter.Key.ToString(),
                EntityType = nameof(PlanningIntervalIteration),
                AuxKey = x.pi.Key.ToString()
            })
            .Take(max)
            .ToListAsync(cancellationToken);

        categories.Add(new GlobalSearchCategoryDto
        {
            Name = "PI Iterations",
            Slug = "pi-iterations",
            Items = iterations,
            TotalCount = iterationCount
        });

        // Sprints
        var sprintQuery = planningDbContext.Iterations
            .Where(i => i.Type == IterationType.Sprint && i.Name.Contains(term));

        var sprintCount = await sprintQuery.CountAsync(cancellationToken);
        var sprints = await sprintQuery
            .OrderBy(i => i.Name)
            .Select(i => new GlobalSearchResultItemDto
            {
                Title = i.Name,
                Subtitle = i.Team != null ? i.Team.Name : null,
                Key = i.Key.ToString(),
                EntityType = nameof(Iteration)
            })
            .Take(max)
            .ToListAsync(cancellationToken);

        categories.Add(new GlobalSearchCategoryDto
        {
            Name = "Sprints",
            Slug = "sprints",
            Items = sprints,
            TotalCount = sprintCount
        });

        // PI Teams
        var piTeamQuery = planningDbContext.PlanningIntervals
            .SelectMany(pi => pi.Teams, (pi, pit) => new { pi, pit.Team })
            .Where(x => x.Team.Name.Contains(term) || ((string)x.Team.Code).Contains(term));

        var today = dateTimeProvider.Today;

        var piTeamCount = await piTeamQuery.CountAsync(cancellationToken);
        var piTeams = await piTeamQuery
            .OrderBy(x => x.pi.DateRange.End < today ? 1      // Past
                        : x.pi.DateRange.Start > today ? 2    // Future
                        : 0)                                   // Active
            .ThenByDescending(x => x.pi.DateRange.Start)
            .Select(x => new GlobalSearchResultItemDto
            {
                Title = x.Team.Name,
                Subtitle = x.pi.Name + " - Plan Review",
                Key = ((string)x.Team.Code),
                EntityType = "PiTeam",
                AuxKey = x.pi.Key.ToString() + "|" + ((string)x.Team.Code).ToLower()
            })
            .Take(max)
            .ToListAsync(cancellationToken);

        categories.Add(new GlobalSearchCategoryDto
        {
            Name = "PI Teams",
            Slug = "pi-teams",
            Items = piTeams,
            TotalCount = piTeamCount
        });

        // Roadmaps (respect visibility: public or current user is a manager)
        var currentUserEmployeeId = Guard.Against.NullOrEmpty(currentUser.GetEmployeeId());
        var publicVisibility = Visibility.Public;
        var roadmapQuery = planningDbContext.Roadmaps
            .Where(r => r.Visibility == publicVisibility || r.RoadmapManagers.Any(m => m.ManagerId == currentUserEmployeeId))
            .Where(r => r.Name.Contains(term));

        var roadmapCount = await roadmapQuery.CountAsync(cancellationToken);
        var roadmaps = await roadmapQuery
            .OrderBy(r => r.Name)
            .Select(r => new GlobalSearchResultItemDto
            {
                Title = r.Name,
                Subtitle = null,
                Key = r.Key.ToString(),
                EntityType = nameof(Roadmap)
            })
            .Take(max)
            .ToListAsync(cancellationToken);

        categories.Add(new GlobalSearchCategoryDto
        {
            Name = "Roadmaps",
            Slug = "roadmaps",
            Items = roadmaps,
            TotalCount = roadmapCount
        });

        // PI Team Objectives (names come from Goals service)
        var objectiveResults = await SearchObjectives(term, max, cancellationToken);
        categories.Add(objectiveResults);

        return new ServiceSearchResponse { Categories = categories };
    }

    private async Task<GlobalSearchCategoryDto> SearchObjectives(string term, int max, CancellationToken cancellationToken)
    {
        // Search objective names via Goals service
        var matchingObjectives = await dispatcher.Send(
            new SearchObjectivesByNameQuery(term, max), cancellationToken);

        if (matchingObjectives.Count == 0)
        {
            return new GlobalSearchCategoryDto
            {
                Name = "PI Objectives",
                Slug = "pi-objectives",
                Items = [],
                TotalCount = 0
            };
        }

        // Get PI objective records for the matching objectives to build URLs
        var objectiveIds = matchingObjectives.Select(o => o.Id).ToList();
        var piObjectives = await planningDbContext.PlanningIntervalObjectives
            .Include(po => po.Team)
            .Where(po => objectiveIds.Contains(po.ObjectiveId))
            .Select(po => new
            {
                po.ObjectiveId,
                po.Key,
                po.PlanningIntervalId,
                TeamName = po.Team.Name,
            })
            .ToListAsync(cancellationToken);

        // Get PI keys for URL building
        var piIds = piObjectives.Select(po => po.PlanningIntervalId).Distinct().ToList();
        var piKeys = await planningDbContext.PlanningIntervals
            .Where(pi => piIds.Contains(pi.Id))
            .Select(pi => new { pi.Id, pi.Key })
            .ToDictionaryAsync(pi => pi.Id, pi => pi.Key, cancellationToken);

        var items = new List<GlobalSearchResultItemDto>();
        foreach (var objective in matchingObjectives)
        {
            var piObjective = piObjectives.FirstOrDefault(po => po.ObjectiveId == objective.Id);
            if (piObjective is null) continue;

            if (!piKeys.TryGetValue(piObjective.PlanningIntervalId, out var piKey)) continue;

            items.Add(new GlobalSearchResultItemDto
            {
                Title = objective.Name,
                Subtitle = piObjective.TeamName,
                Key = piObjective.Key.ToString(),
                EntityType = nameof(PlanningIntervalObjective),
                AuxKey = piKey.ToString()
            });
        }

        return new GlobalSearchCategoryDto
        {
            Name = "PI Objectives",
            Slug = "pi-objectives",
            Items = items,
            TotalCount = matchingObjectives.Count
        };
    }
}
