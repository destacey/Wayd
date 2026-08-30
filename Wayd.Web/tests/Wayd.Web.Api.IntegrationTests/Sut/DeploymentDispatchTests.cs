using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application;
using Wayd.ProductManagement.Application.DeploymentEnvironments.Commands;
using Wayd.ProductManagement.Application.Deployments.Commands;
using Wayd.ProductManagement.Application.Deployments.Queries;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.ReleasePackages.Commands;
using Wayd.ProductManagement.Application.Releases.Commands;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Runs the deployment slice through the real pipeline against SQL Server.
/// </summary>
/// <remarks>
/// These are the rows the delivery measures read, and the projection recomputes Outcome, IsComplete and
/// IsChangeFailure from real columns because the aggregate's versions are Ignore()d on the model. A
/// predicate EF cannot translate, or one that disagrees with the domain, only shows up here.
/// </remarks>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class DeploymentDispatchTests(WaydSqlServerApiFactory factory)
{
    private readonly WaydSqlServerApiFactory _factory = factory;

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}"[..24];

    private sealed record Fixture(Guid ReleaseId, Guid EnvironmentId);

    private static async Task<Fixture> Arrange(
        IDispatcher dispatcher, IProductManagementDbContext dbContext, EnvironmentCategory category)
    {
        var productTypeId = await dbContext.ProductTypes
            .Where(t => t.IsActive && t.IsReleasable)
            .Select(t => t.Id)
            .FirstAsync(TestContext.Current.CancellationToken);

        var product = await dispatcher.Send(
            new CreateProductCommand(Unique("Node"), null, productTypeId, null, null),
            TestContext.Current.CancellationToken);
        Assert.True(product.IsSuccess, product.IsFailure ? product.Error : null);

        var release = await dispatcher.Send(
            new PlanReleaseCommand(product.Value.Id, "4.8.2", null, null, null),
            TestContext.Current.CancellationToken);
        Assert.True(release.IsSuccess, release.IsFailure ? release.Error : null);

        var environment = await dispatcher.Send(
            new CreateDeploymentEnvironmentCommand(Unique("Env"), category, 3),
            TestContext.Current.CancellationToken);
        Assert.True(environment.IsSuccess, environment.IsFailure ? environment.Error : null);

        return new Fixture(release.Value.Id, environment.Value.Id);
    }

    [Fact]
    public async Task Dispatch_StartDeploymentCommand_FreezesTheEnvironmentCategory()
    {
        // Arrange
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IProductManagementDbContext>();

        var fixture = await Arrange(dispatcher, dbContext, EnvironmentCategory.Production);

        // Act
        var started = await dispatcher.Send(
            new StartDeploymentCommand(fixture.ReleaseId, null, fixture.EnvironmentId, "4.8.2.008", null),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(started.IsSuccess, started.IsFailure ? started.Error : null);

        var deployment = await dispatcher.Send(
            new GetDeploymentQuery(started.Value.Id), TestContext.Current.CancellationToken);

        Assert.NotNull(deployment);
        Assert.Equal(EnvironmentCategory.Production, deployment!.EnvironmentCategory);
        Assert.Equal(ProductStatusAlias.InProgress, deployment.Outcome);
        Assert.False(deployment.IsComplete);
        Assert.False(deployment.IsChangeFailure);
    }

    [Fact]
    public async Task Dispatch_FailDeploymentCommand_CountsAsAChangeFailureOnlyInProduction()
    {
        // Arrange
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IProductManagementDbContext>();

        var production = await Arrange(dispatcher, dbContext, EnvironmentCategory.Production);
        var staging = await Arrange(dispatcher, dbContext, EnvironmentCategory.Staging);

        var inProduction = await dispatcher.Send(
            new StartDeploymentCommand(production.ReleaseId, null, production.EnvironmentId, null, null),
            TestContext.Current.CancellationToken);
        Assert.True(inProduction.IsSuccess, inProduction.IsFailure ? inProduction.Error : null);

        var inStaging = await dispatcher.Send(
            new StartDeploymentCommand(staging.ReleaseId, null, staging.EnvironmentId, null, null),
            TestContext.Current.CancellationToken);
        Assert.True(inStaging.IsSuccess, inStaging.IsFailure ? inStaging.Error : null);

        // Act
        await dispatcher.Send(
            new FailDeploymentCommand(inProduction.Value.Id, "Migration timed out.", null),
            TestContext.Current.CancellationToken);
        await dispatcher.Send(
            new FailDeploymentCommand(inStaging.Value.Id, "Smoke test failed.", null),
            TestContext.Current.CancellationToken);

        // Assert
        // A failure caught before production is a failure that was prevented; counting it would invert
        // what the measure means. The projection recomputes this in SQL, so it must agree with the
        // domain predicate.
        var failedInProduction = await dispatcher.Send(
            new GetDeploymentQuery(inProduction.Value.Id), TestContext.Current.CancellationToken);
        var failedInStaging = await dispatcher.Send(
            new GetDeploymentQuery(inStaging.Value.Id), TestContext.Current.CancellationToken);

        Assert.True(failedInProduction!.IsChangeFailure);
        Assert.False(failedInStaging!.IsChangeFailure);
        Assert.Equal(ProductStatusAlias.Failed, failedInStaging.Outcome);
    }

    [Fact]
    public async Task Dispatch_RollBackDeploymentCommand_RefusesAFailedDeployment()
    {
        // Arrange
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IProductManagementDbContext>();

        var fixture = await Arrange(dispatcher, dbContext, EnvironmentCategory.Production);

        var started = await dispatcher.Send(
            new StartDeploymentCommand(fixture.ReleaseId, null, fixture.EnvironmentId, null, null),
            TestContext.Current.CancellationToken);
        Assert.True(started.IsSuccess, started.IsFailure ? started.Error : null);

        await dispatcher.Send(
            new FailDeploymentCommand(started.Value.Id, null, null), TestContext.Current.CancellationToken);

        // Act
        var rolledBack = await dispatcher.Send(
            new RollBackDeploymentCommand(started.Value.Id, null, null), TestContext.Current.CancellationToken);

        // Assert
        // It never reached its environment, so counting it as a rollback would record two failures for
        // one attempt and inflate change failure rate.
        Assert.True(rolledBack.IsFailure);
        Assert.Contains("never reached its environment", rolledBack.Error);
    }

    [Fact]
    public async Task Dispatch_RollBackDeploymentCommand_CountsASucceededDeploymentAsAFailure()
    {
        // Arrange
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IProductManagementDbContext>();

        var fixture = await Arrange(dispatcher, dbContext, EnvironmentCategory.Production);

        var started = await dispatcher.Send(
            new StartDeploymentCommand(fixture.ReleaseId, null, fixture.EnvironmentId, null, null),
            TestContext.Current.CancellationToken);
        Assert.True(started.IsSuccess, started.IsFailure ? started.Error : null);

        var completedAt = SystemClock.Instance.GetCurrentInstant();
        var succeeded = await dispatcher.Send(
            new SucceedDeploymentCommand(started.Value.Id, completedAt), TestContext.Current.CancellationToken);
        Assert.True(succeeded.IsSuccess, succeeded.IsFailure ? succeeded.Error : null);

        // Act
        var rolledBack = await dispatcher.Send(
            new RollBackDeploymentCommand(
                started.Value.Id, "Regression found.", completedAt.Plus(Duration.FromHours(2))),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(rolledBack.IsSuccess, rolledBack.IsFailure ? rolledBack.Error : null);

        var deployment = await dispatcher.Send(
            new GetDeploymentQuery(started.Value.Id), TestContext.Current.CancellationToken);

        Assert.Equal(ProductStatusAlias.RolledBack, deployment!.Outcome);
        Assert.True(deployment.IsChangeFailure);
        Assert.True(deployment.IsComplete);
    }

    [Fact]
    public async Task Dispatch_StartDeploymentCommand_RefusesBothAReleaseAndAPackage()
    {
        // Arrange
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IProductManagementDbContext>();

        var fixture = await Arrange(dispatcher, dbContext, EnvironmentCategory.Production);

        var productTypeId = await dbContext.ProductTypes
            .Where(t => t.IsActive && t.IsReleasable)
            .Select(t => t.Id)
            .FirstAsync(TestContext.Current.CancellationToken);

        var component = await dispatcher.Send(
            new CreateProductCommand(Unique("Comp"), null, productTypeId, null, null),
            TestContext.Current.CancellationToken);
        Assert.True(component.IsSuccess, component.IsFailure ? component.Error : null);

        var package = await dispatcher.Send(
            new AssembleReleasePackageCommand("2026.08", null, null,
            [
                new ManifestEntry(component.Value.Id, null, "1.0", ManifestEntryKind.Changed),
            ]),
            TestContext.Current.CancellationToken);
        Assert.True(package.IsSuccess, package.IsFailure ? package.Error : null);

        // Act
        var send = async () => await dispatcher.Send(
            new StartDeploymentCommand(
                fixture.ReleaseId, package.Value.Id, fixture.EnvironmentId, null, null),
            TestContext.Current.CancellationToken);

        // Assert
        // Where a package exists it is the unit that shipped, so accepting both would double-count one
        // pipeline run in deployment frequency. The validator rejects it in the Wolverine middleware,
        // before the handler runs, so this surfaces as a thrown ValidationException rather than a
        // failed Result — the aggregate's own guard is the second line, covered by the unit tests.
        await Assert.ThrowsAsync<Common.Application.Exceptions.ValidationException>(send);
    }
}
