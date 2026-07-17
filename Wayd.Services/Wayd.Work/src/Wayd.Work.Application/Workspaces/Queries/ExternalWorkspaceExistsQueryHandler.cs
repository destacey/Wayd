using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Workspaces.Queries;

public sealed class ExternalWorkspaceExistsQueryHandler(IWorkDbContext workDbContext) : IQueryHandler<ExternalWorkspaceExistsQuery, bool>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;

    public async Task<bool> Handle(ExternalWorkspaceExistsQuery request, CancellationToken cancellationToken)
    {
        return await _workDbContext.Workspaces
            .AnyAsync(w => w.OwnershipInfo.ExternalId == request.ExternalId, cancellationToken);
    }
}