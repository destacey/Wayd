using Wayd.Common.Application.Search;
using Wayd.Common.Application.Search.Dtos;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Domain.Models.Iterations;
using Wayd.Planning.Domain.Models.Roadmaps;

namespace Wayd.Planning.Application.Search;

public sealed class SearchPlanningForGlobalSearchQueryHandler(IPlanningDbContext planningDbContext, IDateTimeProvider dateTimeProvider, ICurrentPrincipal currentPrincipal)
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

        // Roadmaps (respect visibility: public or current user is a manager). An unlinked caller
        // manages nothing and sees public roadmaps only — this used to throw, which took the whole
        // of global search down for anyone without an employee record, not just this one category.
        var employeeId = await currentPrincipal.GetEmployeeId(cancellationToken);
        var publicVisibility = Visibility.Public;

        var visibleRoadmaps = employeeId is { } managerId
            ? planningDbContext.Roadmaps.Where(r => r.Visibility == publicVisibility || r.RoadmapManagers.Any(m => m.ManagerId == managerId))
            : planningDbContext.Roadmaps.Where(r => r.Visibility == publicVisibility);

        var roadmapQuery = visibleRoadmaps
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

        // PI Objectives
        var objectiveQuery = planningDbContext.PlanningIntervals
            .SelectMany(pi => pi.Objectives, (pi, o) => new { pi, o })
            .Where(x => x.o.Name.Contains(term));

        var objectiveCount = await objectiveQuery.CountAsync(cancellationToken);
        var objectives = await objectiveQuery
            .OrderBy(x => x.o.Name)
            .Select(x => new GlobalSearchResultItemDto
            {
                Title = x.o.Name,
                Subtitle = x.o.Team.Name,
                Key = x.o.Key.ToString(),
                EntityType = nameof(PlanningIntervalObjective),
                AuxKey = x.pi.Key.ToString()
            })
            .Take(max)
            .ToListAsync(cancellationToken);

        categories.Add(new GlobalSearchCategoryDto
        {
            Name = "PI Objectives",
            Slug = "pi-objectives",
            Items = objectives,
            TotalCount = objectiveCount
        });

        return new ServiceSearchResponse { Categories = categories };
    }
}
