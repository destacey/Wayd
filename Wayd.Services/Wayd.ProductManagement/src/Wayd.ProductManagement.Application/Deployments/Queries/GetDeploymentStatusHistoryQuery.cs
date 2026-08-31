using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.Deployments.Queries;

/// <summary>
/// Every status change a deployment has been through, newest first.
/// </summary>
/// <remarks>
/// The owner type is supplied here rather than by the caller, so a request can only reach the history
/// of the record named in the route.
/// </remarks>
public sealed record GetDeploymentStatusHistoryQuery(Guid DeploymentId)
    : IQuery<Result<List<StatusTransitionDto>?>>;

public sealed class GetDeploymentStatusHistoryQueryHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusHistoryReader statusHistoryReader)
    : IQueryHandler<GetDeploymentStatusHistoryQuery, Result<List<StatusTransitionDto>?>>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusHistoryReader _statusHistoryReader = statusHistoryReader;

    /// <returns><c>null</c> when no such deployment exists, so the caller can answer 404.</returns>
    public async Task<Result<List<StatusTransitionDto>?>> Handle(
        GetDeploymentStatusHistoryQuery request, CancellationToken cancellationToken)
    {
        var exists = await _productManagementDbContext.Deployments
            .AsNoTracking()
            .AnyAsync(d => d.Id == request.DeploymentId, cancellationToken);

        if (!exists)
        {
            return Result.Success<List<StatusTransitionDto>?>(null);
        }

        var history = await _statusHistoryReader.Read(
            ProductWorkflowOwners.Deployment.Key, request.DeploymentId, cancellationToken);

        return history.IsFailure
            ? Result.Failure<List<StatusTransitionDto>?>(history.Error)
            : Result.Success<List<StatusTransitionDto>?>(history.Value);
    }
}
