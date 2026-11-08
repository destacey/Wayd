using Wayd.Common.Application.Requests.WorkManagement.Dtos;
using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Workflows.Queries;

/// <summary>Reads the Work workflow each work type in a process follows, for the Azure DevOps sync to keep.</summary>
public sealed class GetExternalWorkProcessWorkflowsQueryHandler(IWorkDbContext workDbContext)
    : IQueryHandler<GetExternalWorkProcessWorkflowsQuery, IReadOnlyList<ExternalWorkProcessWorkflowDto>?>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;

    /// <summary>One row per work type in the process, or null when no work process has the id.</summary>
    public async Task<IReadOnlyList<ExternalWorkProcessWorkflowDto>?> Handle(GetExternalWorkProcessWorkflowsQuery request, CancellationToken cancellationToken)
    {
        var exists = await _workDbContext.WorkProcesses
            .AnyAsync(p => p.Id == request.WorkProcessId, cancellationToken);
        if (!exists)
            return null;

        return await _workDbContext.WorkProcesses
            .Where(p => p.Id == request.WorkProcessId)
            .SelectMany(p => p.Schemes)
            .OrderBy(s => s.WorkType!.Name)
            .ThenBy(s => s.Id)
            .Select(s => new ExternalWorkProcessWorkflowDto(
                s.WorkType!.Name,
                s.Workflow == null ? null : s.Workflow.Id,
                s.Workflow == null ? null : s.Workflow.Name,
                s.Workflow == null ? null : s.Workflow.Description))
            .ToListAsync(cancellationToken);
    }
}
