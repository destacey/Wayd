using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Deployments.Queries;

/// <summary>
/// Every status change a deployment has been through, newest first.
/// </summary>
/// <remarks>
/// The owner type is supplied here rather than by the caller, so a request can only reach the history
/// of the record named in the route.
/// </remarks>
public sealed record GetDeploymentStatusHistoryQuery : IQuery<Result<List<StatusTransitionDto>?>>
{
    public GetDeploymentStatusHistoryQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Deployment>();
    }

    public Expression<Func<Deployment, bool>> IdOrKeyFilter { get; }
}

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
        // Resolved to an id rather than only checked for existence: the history is keyed by the
        // record's id, which a request addressing the deployment by key does not carry.
        var deploymentId = await _productManagementDbContext.Deployments
            .AsNoTracking()
            .Where(request.IdOrKeyFilter)
            .Select(d => (Guid?)d.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (deploymentId is null)
        {
            return Result.Success<List<StatusTransitionDto>?>(null);
        }

        var history = await _statusHistoryReader.Read(
            ProductWorkflowOwners.Deployment.Key, deploymentId.Value, cancellationToken);

        return history.IsFailure
            ? Result.Failure<List<StatusTransitionDto>?>(history.Error)
            : Result.Success<List<StatusTransitionDto>?>(history.Value);
    }
}
