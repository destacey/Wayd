using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.StatusWorkflows.Dtos;

namespace Wayd.Common.Application.StatusWorkflows.Queries;

/// <summary>
/// What changing one record type's entry would move, in every scope on the scheme.
/// </summary>
/// <param name="WorkflowId">The entry's new workflow, or <c>null</c> to preview removing it.</param>
public sealed record PreviewWorkflowSchemeEntryQuery(Guid SchemeId, Guid RecordTypeId, Guid? WorkflowId)
    : IQuery<Result<WorkflowSchemeChangePreviewDto>>;

public sealed class PreviewWorkflowSchemeEntryQueryHandler(
    IStatusWorkflowDbContext dbContext,
    IEnumerable<IRecordTypeStatusMigrator> migrators,
    IWorkflowOwnerAvailability ownerAvailability)
    : IQueryHandler<PreviewWorkflowSchemeEntryQuery, Result<WorkflowSchemeChangePreviewDto>>
{
    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly IEnumerable<IRecordTypeStatusMigrator> _migrators = migrators;
    private readonly IWorkflowOwnerAvailability _ownerAvailability = ownerAvailability;

    public async Task<Result<WorkflowSchemeChangePreviewDto>> Handle(PreviewWorkflowSchemeEntryQuery request, CancellationToken cancellationToken)
    {
        var scheme = await _dbContext.StatusWorkflowSchemes.AsNoTracking()
            .Include(s => s.Entries)
            .FirstOrDefaultAsync(s => s.Id == request.SchemeId && !s.IsImplicit, cancellationToken);
        if (scheme is null || !await _ownerAvailability.IsAvailable(scheme.OwnerType, cancellationToken))
        {
            return Result.Failure<WorkflowSchemeChangePreviewDto>("Workflow scheme not found.");
        }

        if (!await _dbContext.RecordTypes.AnyAsync(r => r.Id == request.RecordTypeId && r.OwnerType == scheme.OwnerType, cancellationToken))
        {
            return Result.Failure<WorkflowSchemeChangePreviewDto>("Record type not found.");
        }

        var scopeIds = await _dbContext.AssignedScopes(scheme.Id, cancellationToken);

        return await SchemeRecordMoves.Preview(
            _dbContext, _migrators, scheme.OwnerType,
            [SchemeRecordMoves.ForEntry(scheme, request.RecordTypeId, request.WorkflowId)],
            scopeIds, cancellationToken);
    }
}
