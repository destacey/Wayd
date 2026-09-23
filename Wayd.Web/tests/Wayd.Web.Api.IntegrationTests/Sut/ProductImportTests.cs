using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Imports;
using Wayd.Infrastructure.Auth;
using Wayd.ProductManagement.Application;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.Products.Dtos;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Proves the product import keeps a tree whole on a real provider: a tree with a rejected product saves
/// none of its products, while another tree in the same file saves all of its own — and a file spanning
/// several chunks never splits a tree between them.
/// </summary>
/// <remarks>
/// A child names its parent by a row in the same file, and the pass looks that parent up among the products
/// it created in the same call. Only the runner's real chunking, against a database that keeps what an
/// earlier chunk committed, shows a tree arriving whole.
/// </remarks>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class ProductImportTests(WaydSqlServerApiFactory factory)
{
    private const string UserId = "product-import-test";

    private readonly WaydSqlServerApiFactory _factory = factory;

    private IServiceScope Scope()
    {
        var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>().SetCurrentUserId(UserId);
        return scope;
    }

    private static Task<string> ProductTypeName(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IProductManagementDbContext>().ProductTypes
            .AsNoTracking()
            .Where(t => t.IsActive)
            .Select(t => t.Name)
            .FirstAsync(TestContext.Current.CancellationToken);

    private static ImportProductDto Row(string name, string typeName, string? parentImportId = null) =>
        new(name, null, typeName, parentImportId, null, null, []);

    private static Task<ImportProcess> Import(IServiceScope scope, IEnumerable<(string ImportId, ImportProductDto Row)> rows) =>
        ImportRuns.SubmitAndWait(scope, new ImportProductsCommand(
            [.. rows.Select(r => new SubmittedImportRow<ImportProductDto>(r.ImportId, r.Row))]));

    [Fact]
    public async Task Import_KeepsOutAWholeTreeWhenAnyOfItsProductsIsRejected_AndKeepsTheOtherTrees()
    {
        // Arrange — Alpha's grandchild names a type that does not exist; the file lists it first, and
        // interleaves the two trees, so nothing about the order keeps them apart
        using var scope = Scope();
        var typeName = await ProductTypeName(scope);
        var suffix = $"{Guid.NewGuid():N}"[..8];

        // Act
        var run = await Import(scope,
        [
            ("a11", Row($"Alpha grandchild {suffix}", "No such type", parentImportId: "a1")),
            ("b1", Row($"Beta child {suffix}", typeName, parentImportId: "b")),
            ("a", Row($"Alpha {suffix}", typeName)),
            ("b", Row($"Beta {suffix}", typeName)),
            ("a1", Row($"Alpha child {suffix}", typeName, parentImportId: "a")),
        ]);

        // Assert
        Assert.Equal(ImportProcessStatus.PartiallySucceeded, run.Status);

        var saved = await scope.ServiceProvider.GetRequiredService<IProductManagementDbContext>().Products
            .AsNoTracking()
            .Where(p => p.Name.EndsWith(suffix))
            .Select(p => new { p.Id, p.Name, p.ParentId })
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            new HashSet<string> { $"Beta {suffix}", $"Beta child {suffix}" },
            saved.Select(p => p.Name).ToHashSet());
        var beta = saved.Single(p => p.Name == $"Beta {suffix}");
        Assert.Equal(beta.Id, saved.Single(p => p.Name == $"Beta child {suffix}").ParentId);

        var rows = run.Rows.ToDictionary(r => r.ImportId);
        Assert.Contains("No such type", rows["a11"].Error);
        Assert.Equal("Not applied: another row for the same product tree was rejected (import id 'a11').", rows["a"].Error);
        Assert.Equal("Not applied: another row for the same product tree was rejected (import id 'a11').", rows["a1"].Error);
        Assert.Equal(ImportRowStatus.Succeeded, rows["b"].Status);
        Assert.Equal(ImportRowStatus.Succeeded, rows["b1"].Status);
    }

    [Fact]
    public async Task Import_KeepsEachTreeInOneChunk_WhenTheFileSpansSeveral()
    {
        // Arrange — two trees of 300 products against a chunk size of 500, the rows interleaved and Beta's
        // root listed last. Chunked row by row, the first chunk would carry Beta's children without their
        // root and reject them; chunked by tree, each tree is one chunk and every parent is in reach.
        using var scope = Scope();
        var typeName = await ProductTypeName(scope);
        var suffix = $"{Guid.NewGuid():N}"[..8];
        const int perTree = 300;

        // Every third product hangs off the root; the rest off the product before, so the trees are deep
        // as well as wide.
        IEnumerable<(string, ImportProductDto)> Tree(string tree) =>
            Enumerable.Range(1, perTree - 1).Select(i => (
                $"{tree}{i}",
                Row($"{tree} {i} {suffix}", typeName, parentImportId: i % 3 == 0 || i == 1 ? tree : $"{tree}{i - 1}")));

        var alpha = Tree("a").ToList();
        var beta = Tree("b").ToList();

        List<(string ImportId, ImportProductDto Row)> rows = [("a", Row($"a {suffix}", typeName))];
        for (var i = 0; i < alpha.Count; i++)
        {
            rows.Add(alpha[i]);
            rows.Add(beta[i]);
        }
        rows.Add(("b", Row($"b {suffix}", typeName)));

        // Act
        var run = await Import(scope, rows);

        // Assert
        Assert.Equal(ImportProcessStatus.Succeeded, run.Status);

        var saved = await scope.ServiceProvider.GetRequiredService<IProductManagementDbContext>().Products
            .AsNoTracking()
            .Where(p => p.Name.EndsWith(suffix))
            .Select(p => new { p.Id, p.Name, p.ParentId })
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2 * perTree, saved.Count);

        var createdByImportId = run.Rows.ToDictionary(r => r.ImportId, r => r.CreatedEntityId);
        foreach (var (importId, row) in rows)
        {
            var product = saved.Single(p => p.Id == createdByImportId[importId]);
            Assert.Equal(row.Name, product.Name);
            Assert.Equal(row.ParentImportId is null ? null : createdByImportId[row.ParentImportId], product.ParentId);
        }
    }
}
