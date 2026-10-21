using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Conditions;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Products.Queries;

/// <summary>
/// The workflow a product currently moves through, with its statuses and transitions.
/// </summary>
/// <remarks>
/// Read through the product so it needs only the product's view permission: the workflow is what the
/// product's status history is drawn on, and its reader may not hold the settings permission.
/// </remarks>
public sealed record GetProductStatusWorkflowQuery : IQuery<StatusWorkflowDetailsDto?>
{
    public GetProductStatusWorkflowQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Product>();
    }

    public Expression<Func<Product, bool>> IdOrKeyFilter { get; }
}

public sealed class GetProductStatusWorkflowQueryHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusWorkflowDetailsReader detailsReader,
    ITransitionConditionChecker conditionChecker)
    : IQueryHandler<GetProductStatusWorkflowQuery, StatusWorkflowDetailsDto?>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusWorkflowDetailsReader _detailsReader = detailsReader;
    private readonly ITransitionConditionChecker _conditionChecker = conditionChecker;

    /// <returns><c>null</c> when no such product exists, so the caller can answer 404.</returns>
    public async Task<StatusWorkflowDetailsDto?> Handle(
        GetProductStatusWorkflowQuery request, CancellationToken cancellationToken)
    {
        var workflowId = await _productManagementDbContext.Products
            .Where(request.IdOrKeyFilter)
            .Select(p => (Guid?)p.StatusWorkflowId)
            .FirstOrDefaultAsync(cancellationToken);

        return workflowId is { } id
            ? await _detailsReader.Read(w => w.Id == id, _conditionChecker.IsAvailable, cancellationToken)
            : null;
    }
}
