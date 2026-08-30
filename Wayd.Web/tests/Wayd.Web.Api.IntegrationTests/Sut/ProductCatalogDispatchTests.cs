using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Interfaces;
using Wayd.ProductManagement.Application;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application.DeploymentEnvironments.Commands;
using Wayd.ProductManagement.Application.DeploymentEnvironments.Queries;
using Wayd.ProductManagement.Application.ProductTagCategories.Commands;
using Wayd.ProductManagement.Application.ProductTagCategories.Queries;
using Wayd.ProductManagement.Application.ProductTypes.Commands;
using Wayd.ProductManagement.Application.ProductTypes.Queries;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Runs the catalog slices through the real pipeline against SQL Server.
/// </summary>
/// <remarks>
/// Both queries here use correlated subqueries for their counts, which an in-memory fake cannot
/// exercise the translation of — a shape EF cannot translate throws only against a real provider.
/// </remarks>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class ProductCatalogDispatchTests(WaydSqlServerApiFactory factory)
{
    private readonly WaydSqlServerApiFactory _factory = factory;

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}"[..24];

    [Fact]
    public async Task Dispatch_ProductTypeLifecycle_CreatesUpdatesAndDeletes()
    {
        // Arrange
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var name = Unique("Service");

        // Act
        var created = await dispatcher.Send(
            new CreateProductTypeCommand(name, "Dispatch test.", true, 9), TestContext.Current.CancellationToken);
        Assert.True(created.IsSuccess, created.IsFailure ? created.Error : null);

        var renamed = name + " v2";
        var updated = await dispatcher.Send(
            new UpdateProductTypeCommand(created.Value.Id, renamed, null, false, 9),
            TestContext.Current.CancellationToken);
        Assert.True(updated.IsSuccess, updated.IsFailure ? updated.Error : null);

        var deleted = await dispatcher.Send(
            new DeleteProductTypeCommand(created.Value.Id), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(deleted.IsSuccess, deleted.IsFailure ? deleted.Error : null);

        var types = await dispatcher.Send(new GetProductTypesQuery(), TestContext.Current.CancellationToken);
        Assert.DoesNotContain(types, t => t.Id == created.Value.Id);
    }

    [Fact]
    public async Task Dispatch_GetProductTypesQuery_CountsTheProductsCarryingEachType()
    {
        // Arrange
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IProductManagementDbContext>();

        var created = await dispatcher.Send(
            new CreateProductTypeCommand(Unique("Counted"), null, true, 9), TestContext.Current.CancellationToken);
        Assert.True(created.IsSuccess, created.IsFailure ? created.Error : null);

        var product = await dispatcher.Send(
            new CreateProductCommand(
                Unique("Node"), null, created.Value.Id, null, null),
            TestContext.Current.CancellationToken);
        Assert.True(product.IsSuccess, product.IsFailure ? product.Error : null);

        // Act
        var types = await dispatcher.Send(new GetProductTypesQuery(), TestContext.Current.CancellationToken);

        // Assert
        // The correlated count is what tells an administrator whether deactivating a type would affect
        // anything; it only translates against a real provider.
        var projected = Assert.Single(types.Where(t => t.Id == created.Value.Id));
        Assert.Equal(1, projected.ProductCount);

        // Cleanup: the type is now in use, so it must be emptied before it can be removed.
        await dispatcher.Send(
            new RemoveProductCommand(product.Value.Id),
            TestContext.Current.CancellationToken);
        await dispatcher.Send(
            new DeleteProductTypeCommand(created.Value.Id), TestContext.Current.CancellationToken);

        Assert.False(
            await dbContext.ProductTypes.AnyAsync(t => t.Id == created.Value.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Dispatch_DeleteProductTypeCommand_RefusesATypeInUse()
    {
        // Arrange
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var created = await dispatcher.Send(
            new CreateProductTypeCommand(Unique("InUse"), null, true, 9), TestContext.Current.CancellationToken);
        Assert.True(created.IsSuccess, created.IsFailure ? created.Error : null);

        var product = await dispatcher.Send(
            new CreateProductCommand(
                Unique("Node"), null, created.Value.Id, null, null),
            TestContext.Current.CancellationToken);
        Assert.True(product.IsSuccess, product.IsFailure ? product.Error : null);

        // Act
        var deleted = await dispatcher.Send(
            new DeleteProductTypeCommand(created.Value.Id), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(deleted.IsFailure);
        Assert.Contains("in use", deleted.Error);

        // Cleanup
        await dispatcher.Send(
            new RemoveProductCommand(product.Value.Id),
            TestContext.Current.CancellationToken);
        await dispatcher.Send(
            new DeleteProductTypeCommand(created.Value.Id), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Dispatch_TagCategoryLifecycle_AddsRenamesAndProjectsTags()
    {
        // Arrange
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var categoryName = Unique("Axis");

        var category = await dispatcher.Send(
            new CreateProductTagCategoryCommand(categoryName, "Dispatch test.", true, 9),
            TestContext.Current.CancellationToken);
        Assert.True(category.IsSuccess, category.IsFailure ? category.Error : null);

        // Act
        var tag = await dispatcher.Send(
            new AddProductTagCommand(category.Value.Id, "alpha", null), TestContext.Current.CancellationToken);
        Assert.True(tag.IsSuccess, tag.IsFailure ? tag.Error : null);

        var renamed = await dispatcher.Send(
            new RenameProductTagCommand(category.Value.Id, tag.Value, "beta", "Renamed."),
            TestContext.Current.CancellationToken);
        Assert.True(renamed.IsSuccess, renamed.IsFailure ? renamed.Error : null);

        var categories = await dispatcher.Send(
            new GetProductTagCategoriesQuery(), TestContext.Current.CancellationToken);

        // Assert
        var projected = Assert.Single(categories.Where(c => c.Id == category.Value.Id));
        var projectedTag = Assert.Single(projected.Tags);
        Assert.Equal("beta", projectedTag.Name);
        Assert.Equal(0, projectedTag.ProductCount);

        // Cleanup
        await dispatcher.Send(
            new DeleteProductTagCategoryCommand(category.Value.Id), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Dispatch_AddProductTagCommand_RefusesADuplicateOnTheSameAxis()
    {
        // Arrange
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var category = await dispatcher.Send(
            new CreateProductTagCategoryCommand(Unique("Axis"), null, true, 9),
            TestContext.Current.CancellationToken);
        Assert.True(category.IsSuccess, category.IsFailure ? category.Error : null);

        var first = await dispatcher.Send(
            new AddProductTagCommand(category.Value.Id, "alpha", null), TestContext.Current.CancellationToken);
        Assert.True(first.IsSuccess, first.IsFailure ? first.Error : null);

        // Act
        var duplicate = await dispatcher.Send(
            new AddProductTagCommand(category.Value.Id, "ALPHA", null), TestContext.Current.CancellationToken);

        // Assert
        // Caught by the aggregate rather than by the unique index, so the caller gets a readable
        // message — which only holds if the handler loaded the existing tags.
        Assert.True(duplicate.IsFailure);
        Assert.Contains("already exists", duplicate.Error);

        // Cleanup
        await dispatcher.Send(
            new DeleteProductTagCategoryCommand(category.Value.Id), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Dispatch_GetDeploymentEnvironmentsQuery_ProjectsThroughMapster()
    {
        // Arrange
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var name = Unique("Env");
        var created = await dispatcher.Send(
            new CreateDeploymentEnvironmentCommand(name, EnvironmentCategory.Staging, 2),
            TestContext.Current.CancellationToken);
        Assert.True(created.IsSuccess, created.IsFailure ? created.Error : null);

        // Act
        var environments = await dispatcher.Send(
            new GetDeploymentEnvironmentsQuery(), TestContext.Current.CancellationToken);

        // Assert
        // This projection goes through Mapster with one configured member. A convention mapping that
        // silently dropped a field, or a Map expression EF could not translate, only shows up here.
        var projected = Assert.Single(environments.Where(e => e.Id == created.Value.Id));
        Assert.Equal(name, projected.Name);
        Assert.Equal(EnvironmentCategory.Staging, projected.Category);
        Assert.Equal(2, projected.RingOrder);
        Assert.True(projected.IsActive);
        Assert.NotEqual(0, projected.Key);
        Assert.Equal(0, projected.DeploymentCount);
    }
}
