using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Infrastructure.Auth;
using Wayd.ProductManagement.Application;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.Releases.Commands;
using Wayd.ProductManagement.Application.Releases.Dtos;
using Wayd.ProductManagement.Application.Versions.Commands;
using Wayd.Web.Api.IntegrationTests.Infrastructure;
using NodaTime;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Proves the release import applies release by release on a real provider: a rejected release keeps out
/// only itself and the contents that arrived on its row, not the rest of the file.
/// </summary>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class ReleaseImportTests(WaydSqlServerApiFactory factory)
{
    private const string UserId = "release-import-test";

    private readonly WaydSqlServerApiFactory _factory = factory;

    private static ImportReleaseDto Row(string version, LocalDate? releasedDate, params Guid[] versionIds) =>
        new(version, null, null, new LocalDate(2026, 5, 1), releasedDate, null, null,
            [.. versionIds.Select(id => new ImportReleaseContentDto(ReleaseContentKind.Version, null, id))]);

    [Fact]
    public async Task Import_KeepsOutARejectedReleaseWithItsContents_AndKeepsTheOtherReleases()
    {
        // Arrange — a planned version that has not shipped, which a release may carry but not announce
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
            new CreateProductCommand($"Released {Guid.NewGuid():N}"[..24], null, productTypeId, null, null),
            TestContext.Current.CancellationToken);
        Assert.True(product.IsSuccess, product.IsFailure ? product.Error : null);

        var unshipped = await dispatcher.Send(
            new PlanVersionCommand(product.Value.Id, "9.0", null, null, null), TestContext.Current.CancellationToken);
        Assert.True(unshipped.IsSuccess, unshipped.IsFailure ? unshipped.Error : null);

        var suffix = $"{Guid.NewGuid():N}"[..8];

        // Act — the second release is announced while carrying the unshipped version, so it is refused;
        // the third carries the same version unannounced, which is legitimate
        var run = await ImportRuns.SubmitAndWait(scope, new ImportReleasesCommand(
        [
            new SubmittedImportRow<ImportReleaseDto>("r1", Row($"2026.1-{suffix}", new LocalDate(2026, 5, 2))),
            new SubmittedImportRow<ImportReleaseDto>("r2", Row($"2026.2-{suffix}", new LocalDate(2026, 5, 2), unshipped.Value.Id)),
            new SubmittedImportRow<ImportReleaseDto>("r3", Row($"2026.3-{suffix}", null, unshipped.Value.Id)),
        ]));

        // Assert
        Assert.Equal(ImportProcessStatus.PartiallySucceeded, run.Status);

        var saved = await dbContext.Releases
            .AsNoTracking()
            .Where(r => r.Version.EndsWith(suffix))
            .Select(r => new { r.Version, Carried = r.Versions.Count })
            .ToDictionaryAsync(r => r.Version, r => r.Carried, TestContext.Current.CancellationToken);

        Assert.Equal([$"2026.1-{suffix}", $"2026.3-{suffix}"], saved.Keys.Order());
        Assert.Equal(1, saved[$"2026.3-{suffix}"]);

        var carriers = await dbContext.ReleaseVersions
            .AsNoTracking()
            .CountAsync(v => v.VersionId == unshipped.Value.Id, TestContext.Current.CancellationToken);
        Assert.Equal(1, carriers);

        var rows = run.Rows.ToDictionary(r => r.ImportId);
        Assert.Contains("has not shipped", rows["r2"].Error);
        Assert.Equal(ImportRowStatus.Succeeded, rows["r1"].Status);
        Assert.Equal(ImportRowStatus.Succeeded, rows["r3"].Status);
    }
}
