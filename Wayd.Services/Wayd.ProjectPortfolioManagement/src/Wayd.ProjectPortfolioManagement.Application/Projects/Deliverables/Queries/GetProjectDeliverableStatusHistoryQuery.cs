using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Projects.Models;
using Wayd.ProjectPortfolioManagement.Domain;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Queries;

/// <summary>
/// A deliverable's status changes, newest first. Null when the deliverable does not exist on the project.
/// </summary>
public sealed record GetProjectDeliverableStatusHistoryQuery(ProjectIdOrKey ProjectIdOrKey, Guid DeliverableId)
    : IQuery<Result<List<StatusTransitionDto>?>>;

public sealed class GetProjectDeliverableStatusHistoryQueryHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    IStatusHistoryReader statusHistoryReader)
    : IQueryHandler<GetProjectDeliverableStatusHistoryQuery, Result<List<StatusTransitionDto>?>>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly IStatusHistoryReader _statusHistoryReader = statusHistoryReader;

    public async Task<Result<List<StatusTransitionDto>?>> Handle(GetProjectDeliverableStatusHistoryQuery request, CancellationToken cancellationToken)
    {
        var projects = _ppmDbContext.Projects.Where(request.ProjectIdOrKey.CreateFilter<Domain.Models.Project>());
        var exists = await _ppmDbContext.ProjectDeliverables
            .AnyAsync(d => d.Id == request.DeliverableId && projects.Any(p => p.Id == d.ProjectId), cancellationToken);
        if (!exists)
            return Result.Success<List<StatusTransitionDto>?>(null);

        var history = await _statusHistoryReader.Read(PpmWorkflowOwners.ProjectDeliverable.Key, request.DeliverableId, cancellationToken);

        return history.IsSuccess
            ? Result.Success<List<StatusTransitionDto>?>(history.Value)
            : Result.Failure<List<StatusTransitionDto>?>(history.Error);
    }
}
