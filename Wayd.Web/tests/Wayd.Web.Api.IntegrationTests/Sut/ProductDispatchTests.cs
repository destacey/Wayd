using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.Products.Queries;
using Wayd.ProductManagement.Domain;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Proves the Product slice runs through the real pipeline: dispatch, the Wolverine-generated handler,
/// the status resolver reading the seeded workflow assignment, and the write to real SQL Server.
/// </summary>
/// <remarks>
/// The resolver and the transition log are the parts unit tests cannot reach. The resolver is mocked in
/// handler tests, so only here does the seeded assignment actually get read; and the history is written
/// by BaseDbContext rather than by the aggregate, so only a round-trip shows the rows landing.
/// </remarks>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class ProductDispatchTests(WaydSqlServerApiFactory factory)
{
    private readonly WaydSqlServerApiFactory _factory = factory;

    private static string UniqueName() => $"Checkout {Guid.NewGuid():N}"[..24];

    [Fact]
    public async Task Dispatch_CreateProductCommand_ResolvesTheSeededWorkflowAndPersists()
    {
        // Arrange
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IProductManagementDbContext>();

        var productTypeId = await dbContext.ProductTypes
            .AsNoTracking()
            .Where(t => t.IsActive)
            .Select(t => t.Id)
            .FirstAsync(TestContext.Current.CancellationToken);

        var command = new CreateProductCommand(UniqueName(), "Dispatch test", productTypeId, null, null);

        // Act
        var result = await dispatcher.Send(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : null);

        var persisted = await dbContext.Products
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == result.Value.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(persisted);

        // The opening status came from the seeded workflow, not from a constant — nothing in the
        // handler names a status, so a wrong assignment would surface here as a failure above.
        Assert.NotEqual(Guid.Empty, persisted!.StatusId);
        Assert.NotEqual(0, persisted.Key);
    }

    [Fact]
    public async Task Dispatch_CreateProductCommand_WritesTheOpeningTransition()
    {
        // Arrange
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IProductManagementDbContext>();
        var waydDbContext = scope.ServiceProvider.GetRequiredService<IWaydDbContext>();

        var productTypeId = await dbContext.ProductTypes
            .AsNoTracking()
            .Where(t => t.IsActive)
            .Select(t => t.Id)
            .FirstAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await dispatcher.Send(
            new CreateProductCommand(UniqueName(), null, productTypeId, null, null),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : null);

        // Written by BaseDbContext, not the aggregate: the navigation is deliberately not mapped, so
        // this row only exists if the collection step ran.
        var transitions = await waydDbContext.Database
            .SqlQuery<int>($@"
                SELECT COUNT(*) AS Value
                FROM [StatusWorkflows].[StatusTransitions]
                WHERE [RecordId] = {result.Value.Id}
                  AND [OwnerType] = {ProductWorkflowOwners.Product.Key}")
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, transitions);
    }

    [Fact]
    public async Task Dispatch_ChangeProductStatusCommand_MovesTheProductAndAppendsHistory()
    {
        // Arrange
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IProductManagementDbContext>();
        var resolver = scope.ServiceProvider.GetRequiredService<Common.Application.StatusWorkflows.IStatusResolver>();

        var productTypeId = await dbContext.ProductTypes
            .AsNoTracking()
            .Where(t => t.IsActive)
            .Select(t => t.Id)
            .FirstAsync(TestContext.Current.CancellationToken);

        var created = await dispatcher.Send(
            new CreateProductCommand(UniqueName(), null, productTypeId, null, null),
            TestContext.Current.CancellationToken);
        Assert.True(created.IsSuccess, created.IsFailure ? created.Error : null);

        var workflow = await resolver.ForScope(
            ProductWorkflowOwners.Product.Key, null, TestContext.Current.CancellationToken);
        Assert.True(workflow.IsSuccess, workflow.IsFailure ? workflow.Error : null);

        var current = await dbContext.Products
            .AsNoTracking()
            .Where(p => p.Id == created.Value.Id)
            .Select(p => p.StatusId)
            .SingleAsync(TestContext.Current.CancellationToken);

        var target = workflow.Value.Statuses.First(s => s.Id != current);

        // Act
        var result = await dispatcher.Send(
            new ChangeProductStatusCommand(created.Value.Id, target.Id), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : null);

        var product = await dbContext.Products
            .AsNoTracking()
            .SingleAsync(p => p.Id == created.Value.Id, TestContext.Current.CancellationToken);

        Assert.Equal(target.Id, product.StatusId);
        Assert.Equal(2, product.StatusTransitionCount);
    }

    [Fact]
    public async Task Dispatch_GetProductsQuery_ProjectsTheStatusAlias()
    {
        // Arrange
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IProductManagementDbContext>();

        var productTypeId = await dbContext.ProductTypes
            .AsNoTracking()
            .Where(t => t.IsActive)
            .Select(t => t.Id)
            .FirstAsync(TestContext.Current.CancellationToken);

        var name = UniqueName();
        var created = await dispatcher.Send(
            new CreateProductCommand(name, null, productTypeId, null, null), TestContext.Current.CancellationToken);
        Assert.True(created.IsSuccess, created.IsFailure ? created.Error : null);

        // Act
        var products = await dispatcher.Send(new GetProductsQuery(), TestContext.Current.CancellationToken);

        // Assert
        // StatusAlias is Ignore()d on the model and read through EF.Property in the projection, so a
        // translation mistake would throw here rather than in any unit test. The seeded opening status
        // ("Concept") deliberately carries no alias, so the value asserted is the category, which the
        // projection reads from a real column.
        var projected = Assert.Single(products.Where(p => p.Id == created.Value.Id));
        Assert.Equal(ProductStatusAlias.None, projected.StatusAlias);
        Assert.Equal(StatusCategory.Proposed, projected.StatusCategory);
        Assert.Equal(name, projected.Name);
        Assert.False(string.IsNullOrWhiteSpace(projected.ProductTypeName));
        Assert.False(string.IsNullOrWhiteSpace(projected.StatusName));
    }
}
