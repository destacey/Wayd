using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Interfaces;
using Wayd.ProductManagement.Application;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.Products.Queries;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// The parent projection, which the products tree is built from.
/// </summary>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class ProductParentProjectionTests(WaydSqlServerApiFactory factory)
{
    private readonly WaydSqlServerApiFactory _factory = factory;

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}"[..24];

    [Fact]
    public async Task GetProducts_ShouldCarryTheParentOfAChild()
    {
        // Arrange
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IProductManagementDbContext>();

        var productTypeId = await dbContext.ProductTypes
            .Where(t => t.IsActive)
            .Select(t => t.Id)
            .FirstAsync(TestContext.Current.CancellationToken);

        var parentName = Unique("Suite");
        var parent = await dispatcher.Send(
            new CreateProductCommand(parentName, null, productTypeId, null, null),
            TestContext.Current.CancellationToken);
        Assert.True(parent.IsSuccess, parent.IsFailure ? parent.Error : null);

        var child = await dispatcher.Send(
            new CreateProductCommand(Unique("Checkout"), null, productTypeId, parent.Value.Id, null),
            TestContext.Current.CancellationToken);
        Assert.True(child.IsSuccess, child.IsFailure ? child.Error : null);

        // Act
        var products = await dispatcher.Send(new GetProductsQuery(), TestContext.Current.CancellationToken);

        // Assert
        var projectedChild = Assert.Single(products, p => p.Id == child.Value.Id);

        Assert.NotNull(projectedChild.Parent);
        Assert.Equal(parent.Value.Id, projectedChild.Parent!.Id);
        Assert.Equal(parent.Value.Key, projectedChild.Parent.Key);
        Assert.Equal(parentName, projectedChild.Parent.Name);

        var projectedParent = Assert.Single(products, p => p.Id == parent.Value.Id);
        Assert.Null(projectedParent.Parent);
    }
}
