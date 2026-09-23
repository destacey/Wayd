using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Infrastructure.Auth;
using Wayd.ProductManagement.Application;
using Wayd.ProductManagement.Application.DeploymentEnvironments.Commands;
using Wayd.ProductManagement.Application.DeploymentEnvironments.Dtos;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Proves the environment import applies environment by environment on a real provider: a rejected
/// environment keeps out only itself.
/// </summary>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class DeploymentEnvironmentImportTests(WaydSqlServerApiFactory factory)
{
    private const string UserId = "environment-import-test";

    private readonly WaydSqlServerApiFactory _factory = factory;

    [Fact]
    public async Task Import_KeepsOutARejectedEnvironment_AndKeepsTheOthers()
    {
        // Arrange — an environment that already exists, so the file's row of the same name is refused
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>().SetCurrentUserId(UserId);

        var suffix = $"{Guid.NewGuid():N}"[..8];

        var existing = await scope.ServiceProvider.GetRequiredService<IDispatcher>().Send(
            new CreateDeploymentEnvironmentCommand($"Staging {suffix}", EnvironmentCategory.Testing, 2),
            TestContext.Current.CancellationToken);
        Assert.True(existing.IsSuccess, existing.IsFailure ? existing.Error : null);

        // Act — the retired row walks a real transition before the rejection, and is still kept
        var run = await ImportRuns.SubmitAndWait(scope, new ImportDeploymentEnvironmentsCommand(
        [
            new SubmittedImportRow<ImportDeploymentEnvironmentDto>("dev", new($"Dev {suffix}", EnvironmentCategory.Development, 1, false)),
            new SubmittedImportRow<ImportDeploymentEnvironmentDto>("stg", new($"Staging {suffix}", EnvironmentCategory.Testing, 2, true)),
            new SubmittedImportRow<ImportDeploymentEnvironmentDto>("prd", new($"Prod {suffix}", EnvironmentCategory.Production, 3, true)),
        ]));

        // Assert
        Assert.Equal(ImportProcessStatus.PartiallySucceeded, run.Status);

        var saved = await scope.ServiceProvider.GetRequiredService<IProductManagementDbContext>().DeploymentEnvironments
            .AsNoTracking()
            .Where(e => e.Name.EndsWith(suffix))
            .Select(e => new { e.Id, e.Name, e.IsActive })
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal([$"Dev {suffix}", $"Prod {suffix}", $"Staging {suffix}"], saved.Select(e => e.Name).Order());
        Assert.Equal(existing.Value.Id, saved.Single(e => e.Name == $"Staging {suffix}").Id);
        Assert.False(saved.Single(e => e.Name == $"Dev {suffix}").IsActive);

        var rows = run.Rows.ToDictionary(r => r.ImportId);
        Assert.Contains("already exists", rows["stg"].Error);
        Assert.Equal(ImportRowStatus.Succeeded, rows["dev"].Status);
        Assert.Equal(ImportRowStatus.Succeeded, rows["prd"].Status);
    }
}
