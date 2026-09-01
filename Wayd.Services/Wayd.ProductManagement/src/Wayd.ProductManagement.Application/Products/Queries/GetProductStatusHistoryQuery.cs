using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Products.Queries;

/// <summary>
/// Every status change a product has been through, newest first.
/// </summary>
/// <remarks>
/// Resolves the record, then reads the shared history. The owner type is supplied here rather than by
/// the caller, so a request can only reach the history of the record named in the route.
/// </remarks>
public sealed record GetProductStatusHistoryQuery : IQuery<Result<List<StatusTransitionDto>?>>
{
    public GetProductStatusHistoryQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Product>();
    }

    public Expression<Func<Product, bool>> IdOrKeyFilter { get; }
}

public sealed class GetProductStatusHistoryQueryHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusHistoryReader statusHistoryReader)
    : IQueryHandler<GetProductStatusHistoryQuery, Result<List<StatusTransitionDto>?>>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusHistoryReader _statusHistoryReader = statusHistoryReader;

    /// <returns>
    /// <c>null</c> when no such product exists, so the caller can answer 404: a product with no
    /// transitions is a different answer from a product that is not there.
    /// </returns>
    public async Task<Result<List<StatusTransitionDto>?>> Handle(
        GetProductStatusHistoryQuery request, CancellationToken cancellationToken)
    {
        var productId = await _productManagementDbContext.Products
            .AsNoTracking()
            .Where(request.IdOrKeyFilter)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (productId is null)
        {
            return Result.Success<List<StatusTransitionDto>?>(null);
        }

        var history = await _statusHistoryReader.Read(
            ProductWorkflowOwners.Product.Key, productId.Value, cancellationToken);

        return history.IsFailure
            ? Result.Failure<List<StatusTransitionDto>?>(history.Error)
            : Result.Success<List<StatusTransitionDto>?>(history.Value);
    }
}
