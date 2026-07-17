using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Workspaces.Queries;

public sealed class WorkspaceKeyExistsQueryHandler(IWorkDbContext workDbContext) : IQueryHandler<WorkspaceKeyExistsQuery, bool>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;

    public async Task<bool> Handle(WorkspaceKeyExistsQuery request, CancellationToken cancellationToken)
    {
        return await _workDbContext.Workspaces
            .AnyAsync(w => w.Key == request.WorkspaceKey, cancellationToken);
    }
}