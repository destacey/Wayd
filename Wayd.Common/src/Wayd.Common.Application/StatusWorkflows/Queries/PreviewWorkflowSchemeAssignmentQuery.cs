using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.StatusWorkflows.Dtos;

namespace Wayd.Common.Application.StatusWorkflows.Queries;

/// <summary>What pointing a scope at another scheme would move, in that scope only.</summary>
public sealed record PreviewWorkflowSchemeAssignmentQuery(Guid AssignmentId, Guid SchemeId)
    : IQuery<Result<WorkflowSchemeChangePreviewDto>>;

public sealed class PreviewWorkflowSchemeAssignmentQueryHandler(
    IStatusWorkflowDbContext dbContext,
    IEnumerable<IRecordTypeStatusMigrator> migrators,
    IWorkflowOwnerAvailability ownerAvailability)
    : IQueryHandler<PreviewWorkflowSchemeAssignmentQuery, Result<WorkflowSchemeChangePreviewDto>>
{
    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly IEnumerable<IRecordTypeStatusMigrator> _migrators = migrators;
    private readonly IWorkflowOwnerAvailability _ownerAvailability = ownerAvailability;

    public async Task<Result<WorkflowSchemeChangePreviewDto>> Handle(PreviewWorkflowSchemeAssignmentQuery request, CancellationToken cancellationToken)
    {
        var assignment = await _dbContext.WorkflowAssignments.AsNoTracking()
            .Include(a => a.Scheme)
                .ThenInclude(s => s!.Entries)
            .FirstOrDefaultAsync(a => a.Id == request.AssignmentId, cancellationToken);
        if (assignment is null || !await _ownerAvailability.IsAvailable(assignment.OwnerType, cancellationToken))
        {
            return Result.Failure<WorkflowSchemeChangePreviewDto>("Workflow assignment not found.");
        }

        var target = await _dbContext.StatusWorkflowSchemes.AsNoTracking()
            .Include(s => s.Entries)
            .FirstOrDefaultAsync(s => s.Id == request.SchemeId && !s.IsImplicit && s.OwnerType == assignment.OwnerType, cancellationToken);
        if (target is null)
        {
            return Result.Failure<WorkflowSchemeChangePreviewDto>("Workflow scheme not found.");
        }

        return await SchemeRecordMoves.Preview(
            _dbContext, _migrators, assignment.OwnerType,
            SchemeRecordMoves.ForAssignment(assignment.Scheme!, target),
            [assignment.ScopeId], cancellationToken);
    }
}
