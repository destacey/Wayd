using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Infrastructure.Auth;
using Wayd.ProductManagement.Application;
using Wayd.ProductManagement.Application.DeploymentEnvironments.Commands;
using Wayd.ProductManagement.Application.Deployments.Commands;
using Wayd.ProductManagement.Application.Deployments.Dtos;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.Versions.Commands;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Proves the deployment import applies row by row on a real provider: a rejected deployment keeps out only
/// itself, and one refused partway through its walk leaves nothing of itself behind.
/// </summary>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class DeploymentImportTests(WaydSqlServerApiFactory factory)
{
    private const string UserId = "deployment-import-test";

    private static readonly Instant Started = Instant.FromUtc(2026, 4, 20, 9, 0);

    private readonly WaydSqlServerApiFactory _factory = factory;

    private static ImportDeploymentDto Row(Guid versionId, string environmentName, string artifactId) =>
        new(versionId, null, environmentName, artifactId, Started, ImportDeploymentOutcome.Succeeded,
            Started.Plus(Duration.FromMinutes(10)), null, null);

    [Fact]
    public async Task Import_KeepsOutARejectedDeployment_AndKeepsTheOthers()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>().SetCurrentUserId(UserId);
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IProductManagementDbContext>();

        var productTypeId = await dbContext.ProductTypes
            .AsNoTracking()
            .Where(t => t.IsActive && t.IsReleasable)
            .Select(t => t.Id)
            .FirstAsync(TestContext.Current.CancellationToken);

        var product = await dispatcher.Send(
            new CreateProductCommand($"Deployed {Guid.NewGuid():N}"[..24], null, productTypeId, null, null),
            TestContext.Current.CancellationToken);
        Assert.True(product.IsSuccess, product.IsFailure ? product.Error : null);

        var version = await dispatcher.Send(
            new PlanVersionCommand(product.Value.Id, "4.8.2", null, null, null), TestContext.Current.CancellationToken);
        Assert.True(version.IsSuccess, version.IsFailure ? version.Error : null);

        var environmentName = $"Prod {Guid.NewGuid():N}"[..20];
        var environment = await dispatcher.Send(
            new CreateDeploymentEnvironmentCommand(environmentName, EnvironmentCategory.Production, 3),
            TestContext.Current.CancellationToken);
        Assert.True(environment.IsSuccess, environment.IsFailure ? environment.Error : null);

        // Act — the second row names an environment that does not exist; the third finishes before it
        // started, which the domain refuses only after the deployment has been created and walked partway
        var run = await ImportRuns.SubmitAndWait(scope, new ImportDeploymentsCommand(
        [
            new SubmittedImportRow<ImportDeploymentDto>("d1", Row(version.Value.Id, environmentName, "4.8.2.001")),
            new SubmittedImportRow<ImportDeploymentDto>("d2", Row(version.Value.Id, "No such environment", "4.8.2.002")),
            new SubmittedImportRow<ImportDeploymentDto>("d3", Row(version.Value.Id, environmentName, "4.8.2.003")
                with { CompletedAt = Started.Minus(Duration.FromMinutes(1)) }),
            new SubmittedImportRow<ImportDeploymentDto>("d4", Row(version.Value.Id, environmentName, "4.8.2.004")),
        ]));

        // Assert
        Assert.Equal(ImportProcessStatus.PartiallySucceeded, run.Status);

        var artifacts = await dbContext.Deployments
            .AsNoTracking()
            .Where(d => d.EnvironmentId == environment.Value.Id)
            .Select(d => d.ArtifactId)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["4.8.2.001", "4.8.2.004"], artifacts.Order());

        var rows = run.Rows.ToDictionary(r => r.ImportId);
        Assert.Contains("No environment was found", rows["d2"].Error);
        Assert.Contains("Could not succeed", rows["d3"].Error);
        Assert.Equal(ImportRowStatus.Succeeded, rows["d1"].Status);
        Assert.Equal(ImportRowStatus.Succeeded, rows["d4"].Status);
    }
}
