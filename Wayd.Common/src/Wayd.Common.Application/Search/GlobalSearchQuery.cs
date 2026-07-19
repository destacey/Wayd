using Wayd.Common.Application.Search.Dtos;
using Wayd.Common.Domain.Authorization;

namespace Wayd.Common.Application.Search;

public sealed record GlobalSearchQuery(string SearchTerm, int MaxResultsPerCategory = 5)
    : IQuery<Result<GlobalSearchResultDto>>;

public sealed class GlobalSearchQueryHandler(IDispatcher dispatcher, ICurrentPrincipal currentPrincipal, ILogger<GlobalSearchQueryHandler> logger)
    : IQueryHandler<GlobalSearchQuery, Result<GlobalSearchResultDto>>
{
    private const string AppRequestName = nameof(GlobalSearchQuery);

    public async Task<Result<GlobalSearchResultDto>> Handle(GlobalSearchQuery request, CancellationToken cancellationToken)
    {
        var searchRequest = new ServiceSearchRequest(request.SearchTerm, request.MaxResultsPerCategory);
        var allCategories = new List<GlobalSearchCategoryDto>();

        // Check permissions sequentially — DbContext is not thread-safe
        var canSearchWork = await currentPrincipal.HasAnyPermission([
            ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.WorkItems),
            ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Workspaces)],
            cancellationToken);

        var canSearchOrg = await currentPrincipal.HasAnyPermission([
            ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Teams),
            ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Employees)],
            cancellationToken);

        var canSearchPlanning = await currentPrincipal.HasAnyPermission([
            ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.PlanningIntervals),
            ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.PlanningIntervalObjectives),
            ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Iterations),
            ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Roadmaps)],
            cancellationToken);

        var canSearchPpm = await currentPrincipal.HasAnyPermission([
            ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Projects),
            ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Programs),
            ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.ProjectPortfolios),
            ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.StrategicInitiatives)],
            cancellationToken);

        // Dispatch to each service sequentially (DbContext is not thread-safe)
        if (canSearchWork)
            allCategories.AddRange(await DispatchServiceSearch<SearchWorkForGlobalSearchQuery>(
                new SearchWorkForGlobalSearchQuery(searchRequest), cancellationToken));

        if (canSearchOrg)
            allCategories.AddRange(await DispatchServiceSearch<SearchOrganizationForGlobalSearchQuery>(
                new SearchOrganizationForGlobalSearchQuery(searchRequest), cancellationToken));

        if (canSearchPlanning)
            allCategories.AddRange(await DispatchServiceSearch<SearchPlanningForGlobalSearchQuery>(
                new SearchPlanningForGlobalSearchQuery(searchRequest), cancellationToken));

        if (canSearchPpm)
            allCategories.AddRange(await DispatchServiceSearch<SearchPpmForGlobalSearchQuery>(
                new SearchPpmForGlobalSearchQuery(searchRequest), cancellationToken));

        var result = new GlobalSearchResultDto
        {
            Categories = allCategories.Where(c => c.Items.Count > 0).ToList()
        };

        return Result.Success(result);
    }

    private async Task<IReadOnlyList<GlobalSearchCategoryDto>> DispatchServiceSearch<TQuery>(
        TQuery query, CancellationToken cancellationToken)
        where TQuery : IQuery<ServiceSearchResponse>
    {
        try
        {
            var response = await dispatcher.Send(query, cancellationToken);
            return response.Categories;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{AppRequestName}: Service search {ServiceQuery} failed.", AppRequestName, typeof(TQuery).Name);
            return [];
        }
    }
}

// Query records dispatched to each service — handlers live in their respective Application projects
public sealed record SearchWorkForGlobalSearchQuery(ServiceSearchRequest Request)
    : IQuery<ServiceSearchResponse>, ILongRunningRequest;

public sealed record SearchOrganizationForGlobalSearchQuery(ServiceSearchRequest Request)
    : IQuery<ServiceSearchResponse>, ILongRunningRequest;

public sealed record SearchPlanningForGlobalSearchQuery(ServiceSearchRequest Request)
    : IQuery<ServiceSearchResponse>, ILongRunningRequest;

public sealed record SearchPpmForGlobalSearchQuery(ServiceSearchRequest Request)
    : IQuery<ServiceSearchResponse>, ILongRunningRequest;
