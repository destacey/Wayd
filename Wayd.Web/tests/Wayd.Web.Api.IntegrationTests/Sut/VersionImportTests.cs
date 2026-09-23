using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Infrastructure.Auth;
using Wayd.ProductManagement.Application;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.Versions.Commands;
using Wayd.ProductManagement.Application.Versions.Dtos;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Proves the version import keeps a product's history whole on a real provider: a product with a rejected
/// version saves none of the file's versions for it, while another product in the same file saves all of its
/// own.
/// </summary>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class VersionImportTests(WaydSqlServerApiFactory factory)
{
    private const string UserId = "version-import-test";

    private readonly WaydSqlServerApiFactory _factory = factory;

    private static async Task<Guid> CreateProduct(IServiceScope scope)
    {
        var productTypeId = await scope.ServiceProvider.GetRequiredService<IProductManagementDbContext>().ProductTypes
            .AsNoTracking()
            .Where(t => t.IsActive && t.IsReleasable)
            .Select(t => t.Id)
            .FirstAsync(TestContext.Current.CancellationToken);

        var created = await scope.ServiceProvider.GetRequiredService<IDispatcher>().Send(
            new CreateProductCommand($"Versioned {Guid.NewGuid():N}"[..24], null, productTypeId, null, null),
            TestContext.Current.CancellationToken);
        Assert.True(created.IsSuccess, created.IsFailure ? created.Error : null);

        return created.Value.Id;
    }

    private static ImportVersionDto Row(Guid productId, string number) =>
        new(productId, number, null, null, new LocalDate(2026, 3, 1), new LocalDate(2026, 3, 8), null, null);

    [Fact]
    public async Task Import_KeepsOutEveryVersionOfAProductWithARejectedRow_AndKeepsTheOtherProducts()
    {
        // Arrange — Checkout already has 1.0, so the file's 1.0 is refused, and only after its 2.0 has been
        // staged
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>().SetCurrentUserId(UserId);
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var checkout = await CreateProduct(scope);
        var payments = await CreateProduct(scope);

        var existing = await dispatcher.Send(
            new PlanVersionCommand(checkout, "1.0", null, null, null), TestContext.Current.CancellationToken);
        Assert.True(existing.IsSuccess, existing.IsFailure ? existing.Error : null);

        // Act
        var run = await ImportRuns.SubmitAndWait(scope, new ImportVersionsCommand(
        [
            new SubmittedImportRow<ImportVersionDto>("c2", Row(checkout, "2.0")),
            new SubmittedImportRow<ImportVersionDto>("p1", Row(payments, "1.0")),
            new SubmittedImportRow<ImportVersionDto>("c1", Row(checkout, "1.0")),
            new SubmittedImportRow<ImportVersionDto>("c3", Row(checkout, "3.0")),
            new SubmittedImportRow<ImportVersionDto>("p2", Row(payments, "2.0")),
        ]));

        // Assert
        Assert.Equal(ImportProcessStatus.PartiallySucceeded, run.Status);

        var numbersByProduct = await scope.ServiceProvider.GetRequiredService<IProductManagementDbContext>().Versions
            .AsNoTracking()
            .Where(v => v.ProductId == checkout || v.ProductId == payments)
            .GroupBy(v => v.ProductId)
            .Select(g => new { ProductId = g.Key, Numbers = g.Select(v => v.Number).ToList() })
            .ToDictionaryAsync(g => g.ProductId, g => g.Numbers, TestContext.Current.CancellationToken);

        Assert.Equal(["1.0"], numbersByProduct[checkout]);
        Assert.Equal(["1.0", "2.0"], numbersByProduct[payments].Order());

        var rows = run.Rows.ToDictionary(r => r.ImportId);
        Assert.Contains("already has a version numbered '1.0'", rows["c1"].Error);
        Assert.Equal("Not applied: another row for the same product was rejected (import id 'c1').", rows["c2"].Error);
        Assert.Equal("Not applied: another row for the same product was rejected (import id 'c1').", rows["c3"].Error);
        Assert.Equal(ImportRowStatus.Succeeded, rows["p1"].Status);
        Assert.Equal(ImportRowStatus.Succeeded, rows["p2"].Status);
    }
}
