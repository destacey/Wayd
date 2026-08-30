using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application;
using Wayd.ProductManagement.Application.DeliveryMetrics.Queries;
using Wayd.ProductManagement.Application.DeploymentEnvironments.Commands;
using Wayd.ProductManagement.Application.Deployments.Commands;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.Releases.Commands;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Computes the delivery measures through the real pipeline against SQL Server.
/// </summary>
/// <remarks>
/// The grouping reads StatusAliasValue, and the window filter leans on the covering index over
/// (EnvironmentCategory, StatusAliasValue, CompletedAt). Both are only exercised against a real
/// provider — that index was silently absent until this work, because it had been declared over a
/// property the model ignored.
/// </remarks>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class DeliveryMetricsDispatchTests(WaydSqlServerApiFactory factory)
{
    private readonly WaydSqlServerApiFactory _factory = factory;

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}"[..24];

    /// <summary>
    /// A completed deployment of its own product, so one test's rows cannot be seen by another's
    /// product-scoped query.
    /// </summary>
    private static async Task<Guid> Deploy(
        IDispatcher dispatcher,
        IProductManagementDbContext dbContext,
        EnvironmentCategory category,
        ProductStatusAlias outcome,
        Instant completedAt,
        Guid? productId = null)
    {
        var productTypeId = await dbContext.ProductTypes
            .Where(t => t.IsActive && t.IsReleasable)
            .Select(t => t.Id)
            .FirstAsync(TestContext.Current.CancellationToken);

        if (productId is null)
        {
            var product = await dispatcher.Send(
                new CreateProductCommand(Unique("Node"), null, productTypeId, null, null),
                TestContext.Current.CancellationToken);
            Assert.True(product.IsSuccess, product.IsFailure ? product.Error : null);
            productId = product.Value.Id;
        }

        var release = await dispatcher.Send(
            new PlanReleaseCommand(productId.Value, Unique("v")[..8], null, null, null),
            TestContext.Current.CancellationToken);
        Assert.True(release.IsSuccess, release.IsFailure ? release.Error : null);

        var environment = await dispatcher.Send(
            new CreateDeploymentEnvironmentCommand(Unique("Env"), category, 3),
            TestContext.Current.CancellationToken);
        Assert.True(environment.IsSuccess, environment.IsFailure ? environment.Error : null);

        var started = await dispatcher.Send(
            new StartDeploymentCommand(release.Value.Id, null, environment.Value.Id, null, completedAt),
            TestContext.Current.CancellationToken);
        Assert.True(started.IsSuccess, started.IsFailure ? started.Error : null);

        switch (outcome)
        {
            case ProductStatusAlias.Succeeded:
                Assert.True((await dispatcher.Send(
                    new SucceedDeploymentCommand(started.Value.Id, completedAt),
                    TestContext.Current.CancellationToken)).IsSuccess);
                break;

            case ProductStatusAlias.Failed:
                Assert.True((await dispatcher.Send(
                    new FailDeploymentCommand(started.Value.Id, null, completedAt),
                    TestContext.Current.CancellationToken)).IsSuccess);
                break;

            case ProductStatusAlias.RolledBack:
                Assert.True((await dispatcher.Send(
                    new SucceedDeploymentCommand(started.Value.Id, completedAt),
                    TestContext.Current.CancellationToken)).IsSuccess);
                Assert.True((await dispatcher.Send(
                    new RollBackDeploymentCommand(started.Value.Id, null, completedAt),
                    TestContext.Current.CancellationToken)).IsSuccess);
                break;
        }

        return productId.Value;
    }

    [Fact]
    public async Task Dispatch_GetDeliveryMetricsQuery_CountsFrequencyAndFailureRateForOneProduct()
    {
        // Arrange
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IProductManagementDbContext>();

        var from = Instant.FromUtc(2026, 4, 1, 0, 0, 0);
        var to = from.Plus(Duration.FromDays(10));
        var at = from.Plus(Duration.FromDays(1));

        // Two succeeded, one rolled back, one failed — all production, all one product.
        var productId = await Deploy(dispatcher, dbContext, EnvironmentCategory.Production, ProductStatusAlias.Succeeded, at);
        await Deploy(dispatcher, dbContext, EnvironmentCategory.Production, ProductStatusAlias.Succeeded, at, productId);
        await Deploy(dispatcher, dbContext, EnvironmentCategory.Production, ProductStatusAlias.RolledBack, at, productId);
        await Deploy(dispatcher, dbContext, EnvironmentCategory.Production, ProductStatusAlias.Failed, at, productId);

        // Act
        var metrics = await dispatcher.Send(
            new GetDeliveryMetricsQuery(from, to, productId), TestContext.Current.CancellationToken);

        // Assert
        // Delivered is three: the rollback reached production, the failure did not.
        Assert.Equal(3, metrics.DeploymentFrequency.Count);
        Assert.Equal(0.3, metrics.DeploymentFrequency.PerDay);

        // Failed is two: the failure and the rollback both count against the four completed.
        Assert.Equal(4, metrics.ChangeFailureRate.TotalDeployments);
        Assert.Equal(2, metrics.ChangeFailureRate.FailedDeployments);
        Assert.Equal(0.5, metrics.ChangeFailureRate.Rate);
    }

    [Fact]
    public async Task Dispatch_GetDeliveryMetricsQuery_IgnoresNonProductionAndOutOfWindowDeployments()
    {
        // Arrange
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IProductManagementDbContext>();

        var from = Instant.FromUtc(2026, 6, 1, 0, 0, 0);
        var to = from.Plus(Duration.FromDays(10));

        var productId = await Deploy(
            dispatcher, dbContext, EnvironmentCategory.Production, ProductStatusAlias.Succeeded,
            from.Plus(Duration.FromDays(1)));

        // Staging, and a production one outside the window.
        await Deploy(
            dispatcher, dbContext, EnvironmentCategory.Staging, ProductStatusAlias.Failed,
            from.Plus(Duration.FromDays(2)), productId);
        await Deploy(
            dispatcher, dbContext, EnvironmentCategory.Production, ProductStatusAlias.Succeeded,
            to.Plus(Duration.FromDays(5)), productId);

        // Act
        var metrics = await dispatcher.Send(
            new GetDeliveryMetricsQuery(from, to, productId), TestContext.Current.CancellationToken);

        // Assert
        // A failure caught in staging is a failure that was prevented, so it is out of scope entirely —
        // not merely excluded from the numerator.
        Assert.Equal(1, metrics.DeploymentFrequency.Count);
        Assert.Equal(1, metrics.ChangeFailureRate.TotalDeployments);
        Assert.Equal(0, metrics.ChangeFailureRate.FailedDeployments);
    }

    [Fact]
    public async Task Dispatch_GetDeliveryMetricsQuery_ReportsTheMeasuresItCannotCompute()
    {
        // Arrange
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var from = Instant.FromUtc(2026, 7, 1, 0, 0, 0);

        // Act
        var metrics = await dispatcher.Send(
            new GetDeliveryMetricsQuery(from, from.Plus(Duration.FromDays(7)), Guid.CreateVersion7()),
            TestContext.Current.CancellationToken);

        // Assert
        // Null rate over an empty window is "nothing to judge", which a reader must be able to tell from
        // a rate of zero — and the two unmeasured DORA metrics say so rather than going missing.
        Assert.Null(metrics.ChangeFailureRate.Rate);
        Assert.Equal(0, metrics.DeploymentFrequency.Count);
        Assert.Equal(2, metrics.Unavailable.Count);
        Assert.All(metrics.Unavailable, u => Assert.False(string.IsNullOrWhiteSpace(u.Reason)));
    }
}
