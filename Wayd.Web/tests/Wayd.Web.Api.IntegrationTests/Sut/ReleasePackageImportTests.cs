using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Infrastructure.Auth;
using Wayd.ProductManagement.Application;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.ReleasePackages.Commands;
using Wayd.ProductManagement.Application.ReleasePackages.Dtos;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Proves the release package import applies package by package on a real provider: a rejected package
/// keeps out only itself and its manifest, not the rest of the file.
/// </summary>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class ReleasePackageImportTests(WaydSqlServerApiFactory factory)
{
    private const string UserId = "release-package-import-test";

    private readonly WaydSqlServerApiFactory _factory = factory;

    [Fact]
    public async Task Import_KeepsOutARejectedPackageWithItsManifest_AndKeepsTheOtherPackages()
    {
        // Arrange — a package already imported, so the file's repeat of its version is refused
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>().SetCurrentUserId(UserId);
        var dbContext = scope.ServiceProvider.GetRequiredService<IProductManagementDbContext>();

        var productTypeId = await dbContext.ProductTypes
            .AsNoTracking()
            .Where(t => t.IsActive)
            .Select(t => t.Id)
            .FirstAsync(TestContext.Current.CancellationToken);

        var product = await scope.ServiceProvider.GetRequiredService<IDispatcher>().Send(
            new CreateProductCommand($"Packaged {Guid.NewGuid():N}"[..24], null, productTypeId, null, null),
            TestContext.Current.CancellationToken);
        Assert.True(product.IsSuccess, product.IsFailure ? product.Error : null);

        var suffix = $"{Guid.NewGuid():N}"[..8];

        ImportReleasePackageDto Row(string version, string componentVersion) =>
            new(version, null, null, null, [new(product.Value.Id, componentVersion, ManifestEntryKind.Changed)]);

        var first = await ImportRuns.SubmitAndWait(scope, new ImportReleasePackagesCommand(
            [new SubmittedImportRow<ImportReleasePackageDto>("p0", Row($"PKG-1-{suffix}", "1.0"))]));
        Assert.Equal(ImportProcessStatus.Succeeded, first.Status);

        // Act
        var run = await ImportRuns.SubmitAndWait(scope, new ImportReleasePackagesCommand(
        [
            new SubmittedImportRow<ImportReleasePackageDto>("p2", Row($"PKG-2-{suffix}", "2.0")),
            new SubmittedImportRow<ImportReleasePackageDto>("p1", Row($"PKG-1-{suffix}", "1.1")),
            new SubmittedImportRow<ImportReleasePackageDto>("p3", Row($"PKG-3-{suffix}", "3.0")),
        ]));

        // Assert
        Assert.Equal(ImportProcessStatus.PartiallySucceeded, run.Status);

        var saved = await dbContext.ReleasePackages
            .AsNoTracking()
            .Where(p => p.Version.EndsWith(suffix))
            .Select(p => new { p.Version, Components = p.Components.Select(c => c.Version).ToList() })
            .ToDictionaryAsync(p => p.Version, p => p.Components, TestContext.Current.CancellationToken);

        Assert.Equal([$"PKG-1-{suffix}", $"PKG-2-{suffix}", $"PKG-3-{suffix}"], saved.Keys.Order());
        Assert.Equal(["1.0"], saved[$"PKG-1-{suffix}"]);

        var rows = run.Rows.ToDictionary(r => r.ImportId);
        Assert.Contains("already exists", rows["p1"].Error);
        Assert.Equal(ImportRowStatus.Succeeded, rows["p2"].Status);
        Assert.Equal(ImportRowStatus.Succeeded, rows["p3"].Status);
    }
}
