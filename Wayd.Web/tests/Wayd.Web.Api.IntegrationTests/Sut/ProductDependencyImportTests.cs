using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Imports;
using Wayd.Infrastructure.Auth;
using Wayd.ProductManagement.Application;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.Products.Dtos;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Proves the dependency import keeps a product whole on a real provider: a product with a rejected row
/// saves none of its links, while another product in the same chunk saves all of its own.
/// </summary>
/// <remarks>
/// The unit fakes track nothing, so they cannot show what matters here. The rejected product's aggregate is
/// loaded and changed before the rejection, and only a real change tracker shows that discarding it leaves
/// neither its links nor the events they raised to ride along on the chunk's save.
/// </remarks>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class ProductDependencyImportTests(WaydSqlServerApiFactory factory)
{
    private static readonly TimeSpan RunTimeout = TimeSpan.FromSeconds(60);

    private readonly WaydSqlServerApiFactory _factory = factory;

    private static async Task<Guid> CreateProduct(IDispatcher dispatcher, Guid productTypeId, Guid? parentId = null)
    {
        var created = await dispatcher.Send(
            new CreateProductCommand($"Dep {Guid.NewGuid():N}"[..20], null, productTypeId, parentId, null),
            TestContext.Current.CancellationToken);
        Assert.True(created.IsSuccess, created.IsFailure ? created.Error : null);

        return created.Value.Id;
    }

    private static async Task<ImportProcess> SubmitAndWait(
        IServiceScope scope, params (string ImportId, ImportProductDependencyDto Row)[] rows)
    {
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var importDbContext = scope.ServiceProvider.GetRequiredService<IImportDbContext>();

        var submitted = await dispatcher.Send(
            new ImportProductDependenciesCommand(
                [.. rows.Select(r => new SubmittedImportRow<ImportProductDependencyDto>(r.ImportId, r.Row))]),
            TestContext.Current.CancellationToken);
        Assert.True(submitted.IsSuccess, submitted.IsFailure ? submitted.Error : null);

        var deadline = DateTime.UtcNow + RunTimeout;
        while (DateTime.UtcNow < deadline)
        {
            var process = await importDbContext.ImportProcesses
                .AsNoTracking()
                .Include(p => p.Rows)
                .SingleAsync(p => p.Id == submitted.Value, TestContext.Current.CancellationToken);

            if (process.IsTerminal)
                return process;

            await Task.Delay(200, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"Import {submitted.Value} did not finish within {RunTimeout}.");
    }

    [Fact]
    public async Task Import_KeepsOutEveryLinkOfAProductWithARejectedRow_AndKeepsTheOtherProducts()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>().SetCurrentUserId("dependency-import-test");
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IProductManagementDbContext>();
        var activityLogs = scope.ServiceProvider.GetRequiredService<IActivityLogDbContext>();

        var productTypeId = await dbContext.ProductTypes
            .AsNoTracking()
            .Where(t => t.IsActive)
            .Select(t => t.Id)
            .FirstAsync(TestContext.Current.CancellationToken);

        var storefront = await CreateProduct(dispatcher, productTypeId);
        var storefrontWeb = await CreateProduct(dispatcher, productTypeId, parentId: storefront);
        var identity = await CreateProduct(dispatcher, productTypeId);
        var search = await CreateProduct(dispatcher, productTypeId);
        var billing = await CreateProduct(dispatcher, productTypeId);

        var today = LocalDate.FromDateTime(DateTime.UtcNow);

        // Act — Storefront's second row is composition, which only the domain can refuse, and only after
        // its first row has already been added to the aggregate.
        var run = await SubmitAndWait(scope,
            ("s1", new(storefront, identity, DependencyStrength.Hard, null, today.PlusDays(-30), null)),
            ("s2", new(storefront, storefrontWeb, DependencyStrength.Hard, null, today.PlusDays(-20), null)),
            ("s3", new(storefront, search, DependencyStrength.Soft, null, today.PlusDays(-10), null)),
            ("b1", new(billing, identity, DependencyStrength.Hard, null, today.PlusDays(-30), null)),
            ("b2", new(billing, search, DependencyStrength.Soft, null, today.PlusDays(-30), today.PlusDays(-5))));

        // Assert
        Assert.Equal(ImportProcessStatus.PartiallySucceeded, run.Status);

        var linksByProduct = await dbContext.Products
            .AsNoTracking()
            .Where(p => p.Id == storefront || p.Id == billing)
            .Select(p => new { p.Id, Count = p.Dependencies.Count })
            .ToDictionaryAsync(p => p.Id, p => p.Count, TestContext.Current.CancellationToken);

        Assert.Equal(0, linksByProduct[storefront]);
        Assert.Equal(2, linksByProduct[billing]);

        // The discarded aggregate's events must not have been drained into the activity log either.
        var storefrontLinkEvents = await activityLogs.ActivityLogs
            .AsNoTracking()
            .CountAsync(a => a.AggregateId == storefront && a.EventType.StartsWith("ProductDependency"),
                TestContext.Current.CancellationToken);
        Assert.Equal(0, storefrontLinkEvents);

        var rows = run.Rows.ToDictionary(r => r.ImportId);
        Assert.Contains("composition", rows["s2"].Error);
        Assert.Equal("Not applied: another row for the same product was rejected (import id 's2').", rows["s1"].Error);
        Assert.Equal("Not applied: another row for the same product was rejected (import id 's2').", rows["s3"].Error);
        Assert.Equal(ImportRowStatus.Succeeded, rows["b1"].Status);
        Assert.Equal(ImportRowStatus.Succeeded, rows["b2"].Status);
    }

    [Fact]
    public async Task Import_AppliesAProductsRowsInDateOrderWhateverOrderTheFileListsThem()
    {
        // Arrange — a strength change, written as the screens record it, with the later row first
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>().SetCurrentUserId("dependency-import-test");
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IProductManagementDbContext>();

        var productTypeId = await dbContext.ProductTypes
            .AsNoTracking()
            .Where(t => t.IsActive)
            .Select(t => t.Id)
            .FirstAsync(TestContext.Current.CancellationToken);

        var checkout = await CreateProduct(dispatcher, productTypeId);
        var payments = await CreateProduct(dispatcher, productTypeId);

        var today = LocalDate.FromDateTime(DateTime.UtcNow);
        var changedOn = today.PlusDays(-40);

        // Act
        var run = await SubmitAndWait(scope,
            ("now", new(checkout, payments, DependencyStrength.Hard, null, changedOn, null)),
            ("was", new(checkout, payments, DependencyStrength.Soft, null, today.PlusDays(-200), changedOn.PlusDays(-1))));

        // Assert — in file order the open link would be recorded first and the earlier one refused as overlapping
        Assert.Equal(ImportProcessStatus.Succeeded, run.Status);

        var links = await dbContext.Products
            .AsNoTracking()
            .Where(p => p.Id == checkout)
            .SelectMany(p => p.Dependencies)
            .OrderBy(d => d.Period.Start)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, links.Count);
        Assert.Equal(DependencyStrength.Soft, links[0].Strength);
        Assert.Equal(changedOn.PlusDays(-1), links[0].Period.End);
        Assert.Equal(DependencyStrength.Hard, links[1].Strength);
        Assert.Null(links[1].Period.End);
    }
}
