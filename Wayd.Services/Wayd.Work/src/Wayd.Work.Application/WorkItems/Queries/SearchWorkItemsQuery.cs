using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItems.Dtos;

namespace Wayd.Work.Application.WorkItems.Queries;

public sealed record SearchWorkItemsQuery(string SearchTerm, int Top) : IQuery<Result<IReadOnlyCollection<WorkItemListDto>>>;

internal sealed class SearchWorkItemsQueryHandler(IWorkDbContext workDbContext, ILogger<SearchWorkItemsQueryHandler> logger) : IQueryHandler<SearchWorkItemsQuery, Result<IReadOnlyCollection<WorkItemListDto>>>
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

        // ORDER BY uses the KeyPrefix/KeyNumber persisted computed columns so SQL Server
        // The ParentId covering index (IX_WorkItems_ParentId_Key) makes the self-join a seek.
        return await _workDbContext.WorkItems
            .Where(e => e.Title.Contains(request.SearchTerm)
                || ((string)e.Key).Contains(request.SearchTerm)
                || (e.ParentId.HasValue && ((string)e.Parent!.Key).Contains(request.SearchTerm)))
            .OrderBy(e => EF.Property<string>(e, "KeyPrefix"))
            .ThenBy(e => EF.Property<int>(e, "KeyNumber"))
            .Take(request.Top)
            .ProjectToType<WorkItemListDto>()
            .ToArrayAsync(cancellationToken);
    }
}

