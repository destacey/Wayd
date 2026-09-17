using System.Linq.Expressions;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Models;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Products.Queries;

/// <summary>
/// Retrieves activity history for a product, newest first.
/// </summary>
public sealed record GetProductActivitiesQuery : IQuery<Result<PagedResponse<ActivityLogDto>?>>
{
    public GetProductActivitiesQuery(IdOrKey idOrKey, int page = 1, int pageSize = 50)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Product>();
        Page = page;
        PageSize = pageSize;
    }

    public Expression<Func<Product, bool>> IdOrKeyFilter { get; }
    public int Page { get; }
    public int PageSize { get; }
}

public sealed class GetProductActivitiesQueryHandler(
    IProductManagementDbContext productManagementDbContext,
    IActivityLogReader activityLogReader)
    : IQueryHandler<GetProductActivitiesQuery, Result<PagedResponse<ActivityLogDto>?>>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IActivityLogReader _activityLogReader = activityLogReader;

    public async Task<Result<PagedResponse<ActivityLogDto>?>> Handle(
        GetProductActivitiesQuery request, CancellationToken cancellationToken)
    {
        var productId = await _productManagementDbContext.Products
            .Where(request.IdOrKeyFilter)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (productId is null)
        {
            return Result.Success<PagedResponse<ActivityLogDto>?>(null);
        }

        var activities = await _activityLogReader.Read(
            productId.Value,
            aggregateType: "Product",
            page: request.Page,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken);

        return Result.Success<PagedResponse<ActivityLogDto>?>(await ResolveRaisedOn(activities, cancellationToken));
    }

    /// <summary>
    /// Names the product each related entry was raised on, so the section can say where it came from.
    /// </summary>
    private async Task<PagedResponse<ActivityLogDto>> ResolveRaisedOn(
        PagedResponse<ActivityLogDto> activities, CancellationToken cancellationToken)
    {
        var productIds = activities.Items
            .Where(a => a.IsRelated && a.AggregateType == "Product")
            .Select(a => a.AggregateId)
            .Distinct()
            .ToList();

        if (productIds.Count == 0)
        {
            return activities;
        }

        var products = await _productManagementDbContext.Products
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Key, p.Name })
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        return activities with
        {
            Items = [.. activities.Items.Select(a =>
                a.IsRelated && a.AggregateType == "Product" && products.TryGetValue(a.AggregateId, out var product)
                    ? a with { RaisedOn = NavigationDto.Create(product.Id, product.Key, product.Name) }
                    : a)],
        };
    }
}
