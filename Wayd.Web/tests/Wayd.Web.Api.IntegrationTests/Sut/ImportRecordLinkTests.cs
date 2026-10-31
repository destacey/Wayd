using System.Globalization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Infrastructure.Auth;
using Wayd.ProductManagement.Application;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.Products.Dtos;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Proves every import's lookup of the records its rows created runs on a real provider, and that a run's
/// rows link to the record each one made.
/// </summary>
/// <remarks>
/// The lookups join and project in ways an in-memory fake accepts whatever EF would make of them; only SQL
/// Server shows whether each one translates. A query that cannot be translated throws when it runs, whether
/// or not anything matches, so asking each one about an id nothing has is enough to prove it.
/// </remarks>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class ImportRecordLinkTests(WaydSqlServerApiFactory factory)
{
    private const string UserId = "import-record-link-test";

    private readonly WaydSqlServerApiFactory _factory = factory;

    private IServiceScope Scope()
    {
        var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>().SetCurrentUserId(UserId);
        return scope;
    }

    [Fact]
    public async Task DescribeCreatedRecords_TranslatesForEveryImport()
    {
        // Arrange
        using var scope = Scope();
        var definitions = scope.ServiceProvider.GetRequiredService<IImportDefinitionRegistry>().All;
        Guid[] unknown = [Guid.CreateVersion7()];

        // Act
        var failures = new List<string>();
        foreach (var definition in definitions)
        {
            try
            {
                var links = await definition.DescribeCreatedRecords(unknown, TestContext.Current.CancellationToken);
                if (links.Count != 0)
                    failures.Add($"{definition.Key}: linked an id nothing has.");
            }
            catch (Exception ex)
            {
                failures.Add($"{definition.Key}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // Assert
        definitions.Should().NotBeEmpty();
        failures.Should().BeEmpty();
    }

    [Fact]
    public async Task DescribeCreatedRecords_LinksAnImportedProductByItsKey()
    {
        // Arrange
        using var scope = Scope();
        var typeName = await scope.ServiceProvider.GetRequiredService<IProductManagementDbContext>().ProductTypes
            .AsNoTracking()
            .Where(t => t.IsActive)
            .Select(t => t.Name)
            .FirstAsync(TestContext.Current.CancellationToken);
        var name = $"Linked product {Guid.NewGuid():N}"[..32];

        var run = await ImportRuns.SubmitAndWait(scope, new ImportProductsCommand(
            [new SubmittedImportRow<ImportProductDto>("p1", new(name, null, typeName, null, null, null, [], null))]));
        var createdId = run.Rows.Single().CreatedEntityId!.Value;

        var product = await scope.ServiceProvider.GetRequiredService<IProductManagementDbContext>().Products
            .AsNoTracking()
            .SingleAsync(p => p.Id == createdId, TestContext.Current.CancellationToken);

        var definition = scope.ServiceProvider.GetRequiredService<IImportDefinitionRegistry>()
            .Find(run.ImportType).Value;

        // Act
        var links = await definition.DescribeCreatedRecords([createdId], TestContext.Current.CancellationToken);

        // Assert
        links.Should().ContainSingle().Which.Value.Should().Be(new ImportRecordLink(
            ImportRecordType.Product, product.Key.ToString(CultureInfo.InvariantCulture), name));
    }
}
