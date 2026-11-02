using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.StatusWorkflows.Dtos;

namespace Wayd.Common.Application.StatusWorkflows.Queries;

/// <summary>What changing a scheme's default would move, in every scope on the scheme.</summary>
public sealed record PreviewWorkflowSchemeDefaultQuery(Guid SchemeId, Guid WorkflowId)
    : IQuery<Result<WorkflowSchemeChangePreviewDto>>;

public sealed class PreviewWorkflowSchemeDefaultQueryHandler(
    IStatusWorkflowDbContext dbContext,
    IEnumerable<IRecordTypeStatusMigrator> migrators,
    IWorkflowOwnerAvailability ownerAvailability)
    : IQueryHandler<PreviewWorkflowSchemeDefaultQuery, Result<WorkflowSchemeChangePreviewDto>>
{
    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly IEnumerable<IRecordTypeStatusMigrator> _migrators = migrators;
    private readonly IWorkflowOwnerAvailability _ownerAvailability = ownerAvailability;

    public async Task<Result<WorkflowSchemeChangePreviewDto>> Handle(PreviewWorkflowSchemeDefaultQuery request, CancellationToken cancellationToken)
    {
        var scheme = await _dbContext.StatusWorkflowSchemes.AsNoTracking()
            .Include(s => s.Entries)
            .FirstOrDefaultAsync(s => s.Id == request.SchemeId && !s.IsImplicit, cancellationToken);
        if (scheme is null || !await _ownerAvailability.IsAvailable(scheme.OwnerType, cancellationToken))
        {
            return Result.Failure<WorkflowSchemeChangePreviewDto>("Workflow scheme not found.");
        }

        var scopeIds = await _dbContext.AssignedScopes(scheme.Id, cancellationToken);

        return await SchemeRecordMoves.Preview(
            _dbContext, _migrators, scheme.OwnerType,
            [SchemeRecordMoves.ForDefault(scheme, request.WorkflowId)],
            scopeIds, cancellationToken);
    }
}
