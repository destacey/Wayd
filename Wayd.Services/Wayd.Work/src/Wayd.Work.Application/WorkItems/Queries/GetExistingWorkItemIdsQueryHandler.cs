using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkItems.Queries;

/// <summary>Answers another module asking which work items exist.</summary>
public sealed class GetExistingWorkItemIdsQueryHandler(IWorkDbContext workDbContext)
    : IQueryHandler<GetExistingWorkItemIdsQuery, IReadOnlySet<Guid>>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;

    public async Task<IReadOnlySet<Guid>> Handle(GetExistingWorkItemIdsQuery request, CancellationToken cancellationToken)
    {
        if (request.Ids.Count == 0)
            return new HashSet<Guid>();

        var ids = request.Ids.Distinct().ToList();
        var found = await _workDbContext.WorkItems
            .Where(w => ids.Contains(w.Id))
            .Select(w => w.Id)
            .ToListAsync(cancellationToken);

        return found.ToHashSet();
    }
}
