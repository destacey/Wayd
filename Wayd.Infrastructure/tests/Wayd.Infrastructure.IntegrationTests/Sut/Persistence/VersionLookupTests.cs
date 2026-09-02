using Mapster;
using Mapster.Utils;
using Microsoft.EntityFrameworkCore;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Infrastructure.Persistence.Initialization;
using Wayd.ProductManagement.Application.Versions.Dtos;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

// The delivery artifact record, not System.Version.
using Version = Wayd.ProductManagement.Domain.Models.Version;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Addressing a version by its short key.
/// </summary>
/// <remarks>
/// <c>Version.Key</c> is database-generated, so every version built by a faker carries 0 and a
/// handler test cannot tell a key lookup from a broken one — two versions would both "match" key 0.
/// Only a real round trip assigns distinct keys, which is what these check.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class VersionLookupTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    private static readonly Instant Timestamp = Instant.FromUtc(2026, 3, 4, 10, 0, 0);

    /// <summary>
    /// Registers the module's Mapster mappings as its <c>ConfigureServices</c> does at startup.
    /// Without it a projection falls back to convention and silently drops every configured member.
    /// </summary>
    private static readonly Lazy<bool> Mappings = new(() =>
    {
        var assembly = typeof(VersionDto).Assembly;
        TypeAdapterConfig.GlobalSettings.Scan(assembly);
        TypeAdapterConfig.GlobalSettings.ScanInheritedTypes(assembly);
        return true;
    });

    private static IDateTimeProvider DateTimeProvider()
    {
        var provider = new Mock<IDateTimeProvider>();
        provider.SetupGet(d => d.Now).Returns(Timestamp);
        provider.SetupGet(d => d.Today).Returns(new LocalDate(2026, 3, 4));

        return provider.Object;
    }

    private async Task<StatusWorkflow> VersionWorkflow(WaydDbContext context)
    {
        ProductWorkflowOwners.Register();

        var workflow = await context.StatusWorkflows
            .Include(w => w.Statuses)
            .FirstOrDefaultAsync(
                w => w.OwnerType == ProductWorkflowOwners.Version.Key && w.IsSystem,
                TestContext.Current.CancellationToken);

        if (workflow is null)
        {
            await new ProductManagementWorkflowSeeder()
                .Initialize(context, DateTimeProvider(), TestContext.Current.CancellationToken);

            workflow = await context.StatusWorkflows
                .Include(w => w.Statuses)
                .FirstAsync(
                    w => w.OwnerType == ProductWorkflowOwners.Version.Key && w.IsSystem,
                    TestContext.Current.CancellationToken);
        }

        return workflow;
    }

    private async Task<Version> SeedVersion(WaydDbContext context, string number)
    {
        var workflow = await VersionWorkflow(context);
        var initial = StatusRef.From(workflow.Statuses.OrderBy(s => s.Order).First());

        var productType = await context.ProductTypes
            .Select(t => t.Id)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        if (productType == Guid.Empty)
        {
            await new ProductTypeSeeder()
                .Initialize(context, DateTimeProvider(), TestContext.Current.CancellationToken);

            productType = await context.ProductTypes
                .Select(t => t.Id)
                .FirstAsync(TestContext.Current.CancellationToken);
        }

        var product = Product.Create(
            $"Lookup {Guid.CreateVersion7()}",
            null,
            productType,
            null,
            null,
            initial,
            EventActor.System,
            Timestamp);

        context.Products.Add(product);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var version = Version.Create(
            product.Id,
            number,
            null,
            null,
            null,
            isProductReleasable: true,
            initial,
            product.Name,
            EventActor.System,
            Timestamp).Value;

        context.Versions.Add(version);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return version;
    }

    [Fact]
    public async Task Filter_ShouldFindTheVersionByItsKey()
    {
        // Arrange — two versions, so a filter ignoring the key would match the wrong one rather than
        // none, which is the failure a single-row test cannot see.
        await using var context = _fixture.CreateContext();
        var first = await SeedVersion(context, "1.0");
        var second = await SeedVersion(context, "2.0");

        first.Key.Should().NotBe(second.Key, "the database assigns each version its own key");

        // Act
        await using var reader = _fixture.CreateContext();
        var found = await reader.Versions
            .AsNoTracking()
            .Where(new IdOrKey(second.Key.ToString()).CreateFilter<Version>())
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken);

        // Assert
        found.Should().NotBeNull();
        found!.Id.Should().Be(second.Id);
        found.Number.Should().Be("2.0");
    }

    [Fact]
    public async Task Filter_ShouldStillFindTheVersionByItsId()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var version = await SeedVersion(context, "3.0");

        // Act
        await using var reader = _fixture.CreateContext();
        var found = await reader.Versions
            .AsNoTracking()
            .Where(new IdOrKey(version.Id.ToString()).CreateFilter<Version>())
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken);

        // Assert
        found.Should().NotBeNull();
        found!.Id.Should().Be(version.Id);
    }

    [Fact]
    public async Task Projection_ShouldResolveTheProductThroughItsNavigation()
    {
        // Arrange — the projection reads Product through a navigation, which an in-memory fake leaves
        // null however the mapping is written. Only a real provider translates the join.
        _ = Mappings.Value;
        await using var context = _fixture.CreateContext();
        var version = await SeedVersion(context, "4.0");

        var productName = await context.Products
            .Where(p => p.Id == version.ProductId)
            .Select(p => p.Name)
            .FirstAsync(TestContext.Current.CancellationToken);

        // Act
        await using var reader = _fixture.CreateContext();
        var dto = await reader.Versions
            .AsNoTracking()
            .Where(r => r.Id == version.Id)
            .ProjectToType<VersionDto>()
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        // Assert
        dto.Should().NotBeNull();
        dto!.Product.Should().NotBeNull("a version always has the product it was cut against");
        dto.Product.Id.Should().Be(version.ProductId);
        dto.Product.Name.Should().Be(productName);
        dto.Number.Should().Be("4.0");
        dto.Status.Id.Should().Be(version.StatusId);
        dto.Status.Name.Should().Be(version.StatusName, "the status is flattened across four columns");
        dto.Status.Category.Should().Be(version.StatusCategory);
        dto.Status.Alias.Should().Be(version.StatusAliasValue);
    }
}
