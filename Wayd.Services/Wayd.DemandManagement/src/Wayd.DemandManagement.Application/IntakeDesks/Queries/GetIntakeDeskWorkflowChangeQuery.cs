using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Domain;

namespace Wayd.DemandManagement.Application.IntakeDesks.Queries;

/// <summary>
/// What moving a desk onto another workflow would do: where each status of the one it follows lands, how many
/// of the desk's requests are in each, and the statuses an unplaced one can be sent to.
/// </summary>
/// <remarks>
/// Answered only to those who may change the desk's workflow, since the counts say what is on a private desk.
/// </remarks>
/// <param name="Id">The desk.</param>
/// <param name="WorkflowId">The published intake request workflow the desk would move to.</param>
public sealed record GetIntakeDeskWorkflowChangeQuery(Guid Id, Guid WorkflowId) : IQuery<Result<ScopedWorkflowChangePreviewDto>>;

public sealed class GetIntakeDeskWorkflowChangeQueryHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    IScopedWorkflowAssignments workflowAssignments)
    : IQueryHandler<GetIntakeDeskWorkflowChangeQuery, Result<ScopedWorkflowChangePreviewDto>>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IScopedWorkflowAssignments _workflowAssignments = workflowAssignments;

    public async Task<Result<ScopedWorkflowChangePreviewDto>> Handle(GetIntakeDeskWorkflowChangeQuery request, CancellationToken cancellationToken)
    {
        if (!await _dbContext.IntakeDesks.AnyAsync(d => d.Id == request.Id, cancellationToken))
            return Result.Failure<ScopedWorkflowChangePreviewDto>(IntakeDeskAccess.NotFoundError);

        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);
        if (!reader.IsDeskAdminOn(request.Id))
            return Result.Failure<ScopedWorkflowChangePreviewDto>(IntakeDeskAccess.NotADeskAdminError);

        return await _workflowAssignments.PreviewChange(
            DemandManagementWorkflowOwners.IntakeRequest.Key, request.Id, request.WorkflowId, cancellationToken);
    }
}
