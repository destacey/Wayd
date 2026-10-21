using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Conditions;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Deployments.Queries;

/// <summary>
/// The workflow a deployment currently moves through, with its statuses, transitions and declared moves.
/// </summary>
/// <remarks>
/// Read through the deployment so it needs only the delivery view permission: the workflow is what the
/// deployment's status history is drawn on, and its reader may not hold the settings permission.
/// </remarks>
public sealed record GetDeploymentStatusWorkflowQuery : IQuery<StatusWorkflowDetailsDto?>
{
    public GetDeploymentStatusWorkflowQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Deployment>();
    }

    public Expression<Func<Deployment, bool>> IdOrKeyFilter { get; }
}

public sealed class GetDeploymentStatusWorkflowQueryHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusWorkflowDetailsReader detailsReader,
    ITransitionConditionChecker conditionChecker)
    : IQueryHandler<GetDeploymentStatusWorkflowQuery, StatusWorkflowDetailsDto?>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusWorkflowDetailsReader _detailsReader = detailsReader;
    private readonly ITransitionConditionChecker _conditionChecker = conditionChecker;

    /// <returns><c>null</c> when no such deployment exists, so the caller can answer 404.</returns>
    public async Task<StatusWorkflowDetailsDto?> Handle(
        GetDeploymentStatusWorkflowQuery request, CancellationToken cancellationToken)
    {
        var workflowId = await _productManagementDbContext.Deployments
            .Where(request.IdOrKeyFilter)
            .Select(d => (Guid?)d.StatusWorkflowId)
            .FirstOrDefaultAsync(cancellationToken);

        return workflowId is { } id
            ? await _detailsReader.Read(w => w.Id == id, _conditionChecker.IsAvailable, cancellationToken)
            : null;
    }
}
