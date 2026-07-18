using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkProcesses.Queries;

public sealed class ExternalWorkProcessExistsQueryHandler(IWorkDbContext workDbContext) : IQueryHandler<ExternalWorkProcessExistsQuery, bool>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;

    public async Task<bool> Handle(ExternalWorkProcessExistsQuery request, CancellationToken cancellationToken)
    {
        return await _workDbContext.WorkProcesses
            .AnyAsync(w => w.ExternalId == request.ExternalId, cancellationToken);
    }
}