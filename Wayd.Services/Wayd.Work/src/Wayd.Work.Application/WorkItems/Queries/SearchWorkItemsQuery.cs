using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItems.Dtos;

namespace Wayd.Work.Application.WorkItems.Queries;

public sealed record SearchWorkItemsQuery(string SearchTerm, int Top) : IQuery<Result<IReadOnlyCollection<WorkItemListDto>>>;

public sealed class SearchWorkItemsQueryHandler(IWorkDbContext workDbContext, ILogger<SearchWorkItemsQueryHandler> logger) : IQueryHandler<SearchWorkItemsQuery, Result<IReadOnlyCollection<WorkItemListDto>>>
{
    private const string AppRequestName = nameof(SearchWorkItemsQuery);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<SearchWorkItemsQueryHandler> _logger = logger;

    public async Task<Result<IReadOnlyCollection<WorkItemListDto>>> Handle(SearchWorkItemsQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            _logger.LogWarning("{AppRequestName}: No search term provided. {@Request}", AppRequestName, request);
            return Result.Failure<IReadOnlyCollection<WorkItemListDto>>("No search term provided.");
        }

        return await _workDbContext.SearchWorkItems(request.SearchTerm, request.Top)
            .ProjectToType<WorkItemListDto>()
            .ToArrayAsync(cancellationToken);
    }
}

