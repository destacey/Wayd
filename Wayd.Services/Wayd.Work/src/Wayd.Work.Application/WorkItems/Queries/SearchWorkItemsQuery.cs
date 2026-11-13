using Wayd.Common.Domain.Enums.Work;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItems.Dtos;

namespace Wayd.Work.Application.WorkItems.Queries;

/// <summary>Finds work items across every workspace by key, parent key or title.</summary>
/// <param name="SearchTerm">The text to match.</param>
/// <param name="Top">The most work items to return.</param>
/// <param name="Tier">When set, only work items whose type is in this tier.</param>
public sealed record SearchWorkItemsQuery(string SearchTerm, int Top, WorkTypeTier? Tier = null) : IQuery<Result<IReadOnlyCollection<WorkItemListDto>>>;

public sealed class SearchWorkItemsQueryHandler(IWorkDbContext workDbContext, ILogger<SearchWorkItemsQueryHandler> logger) : IQueryHandler<SearchWorkItemsQuery, Result<IReadOnlyCollection<WorkItemListDto>>>
{
    private const string AppRequestName = nameof(SearchWorkItemsQuery);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<SearchWorkItemsQueryHandler> _logger = logger;

    public async Task<Result<IReadOnlyCollection<WorkItemListDto>>> Handle(SearchWorkItemsQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            _logger.LogWarning("{AppRequestName}: No search term provided.", AppRequestName);
            return Result.Failure<IReadOnlyCollection<WorkItemListDto>>("No search term provided.");
        }

        return await _workDbContext.SearchWorkItems(request.SearchTerm, request.Tier, request.Top)
            .ProjectToType<WorkItemListDto>()
            .ToArrayAsync(cancellationToken);
    }
}

