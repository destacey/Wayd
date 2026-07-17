using Wayd.Common.Application.Search;
using Wayd.Common.Application.Search.Dtos;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Search;

public sealed class SearchWorkForGlobalSearchQueryHandler(IWorkDbContext workDbContext)
    : IQueryHandler<SearchWorkForGlobalSearchQuery, ServiceSearchResponse>
{
    public async Task<ServiceSearchResponse> Handle(SearchWorkForGlobalSearchQuery request, CancellationToken cancellationToken)
    {
        var term = request.Request.SearchTerm;
        var max = request.Request.MaxResultsPerCategory;
        var categories = new List<GlobalSearchCategoryDto>();

        // Work Items
        var workItemQuery = workDbContext.WorkItems
            .Where(w => w.Title.Contains(term) || ((string)w.Key).Contains(term));

        var workItemCount = await workItemQuery.CountAsync(cancellationToken);
        var workItems = await workItemQuery
            .OrderByDescending(w => (string)w.Key)
            .Select(w => new GlobalSearchResultItemDto
            {
                Title = w.Title,
                Subtitle = w.Workspace.Name,
                Key = (string)w.Key,
                EntityType = nameof(WorkItem),
                AuxKey = (string)w.Workspace.Key
            })
            .Take(max)
            .ToListAsync(cancellationToken);

        categories.Add(new GlobalSearchCategoryDto
        {
            Name = "Work Items",
            Slug = "work-items",
            Items = workItems,
            TotalCount = workItemCount
        });

        // Workspaces
        var workspaceQuery = workDbContext.Workspaces
            .Where(ws => !ws.IsDeleted)
            .Where(ws => ws.Name.Contains(term) || ((string)ws.Key).Contains(term));

        var workspaceCount = await workspaceQuery.CountAsync(cancellationToken);
        var workspaces = await workspaceQuery
            .OrderBy(ws => ws.Name)
            .Select(ws => new GlobalSearchResultItemDto
            {
                Title = ws.Name,
                Subtitle = ws.IsActive ? null : "Inactive",
                Key = (string)ws.Key,
                EntityType = nameof(Workspace)
            })
            .Take(max)
            .ToListAsync(cancellationToken);

        categories.Add(new GlobalSearchCategoryDto
        {
            Name = "Workspaces",
            Slug = "workspaces",
            Items = workspaces,
            TotalCount = workspaceCount
        });

        return new ServiceSearchResponse { Categories = categories };
    }
}
