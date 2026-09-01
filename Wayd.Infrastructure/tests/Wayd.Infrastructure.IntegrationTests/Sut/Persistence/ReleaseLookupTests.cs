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
using Wayd.ProductManagement.Application.Releases.Dtos;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Addressing a release by its short key.
/// </summary>
/// <remarks>
/// <c>Release.Key</c> is database-generated, so every release built by a faker carries 0 and a
/// handler test cannot tell a key lookup from a broken one — two releases would both "match" key 0.
/// Only a real round trip assigns distinct keys, which is what these check.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class ReleaseLookupTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    private static readonly Instant Timestamp = Instant.FromUtc(2026, 3, 4, 10, 0, 0);

    /// <summary>
    /// Registers the module's Mapster mappings as its <c>ConfigureServices</c> does at startup.
    /// Without it a projection falls back to convention and silently drops every configured member.
    /// </summary>
    private static readonly Lazy<bool> Mappings = new(() =>
    {
        var assembly = typeof(ReleaseDto).Assembly;
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

    private async Task<StatusWorkflow> ReleaseWorkflow(WaydDbContext context)
    {
        ProductWorkflowOwners.Register();

        var workflow = await context.StatusWorkflows
            .Include(w => w.Statuses)
            .FirstOrDefaultAsync(
                w => w.OwnerType == ProductWorkflowOwners.Release.Key && w.IsSystem,
                TestContext.Current.CancellationToken);

        if (workflow is null)
        {
            await new ProductManagementWorkflowSeeder()
                .Initialize(context, DateTimeProvider(), TestContext.Current.CancellationToken);

            workflow = await context.StatusWorkflows
                .Include(w => w.Statuses)
                .FirstAsync(
                    w => w.OwnerType == ProductWorkflowOwners.Release.Key && w.IsSystem,
                    TestContext.Current.CancellationToken);
        }

        return workflow;
    }

    private async Task<Release> SeedRelease(WaydDbContext context, string version)
    {
        var workflow = await ReleaseWorkflow(context);
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

        var release = Release.Create(
            product.Id,
            version,
            null,
            null,
            null,
            isProductReleasable: true,
            initial,
            product.Name,
            EventActor.System,
            Timestamp).Value;

        context.Releases.Add(release);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return release;
    }

    [Fact]
    public async Task Filter_ShouldFindTheReleaseByItsKey()
    {
        // Arrange — two releases, so a filter ignoring the key would match the wrong one rather than
        // none, which is the failure a single-row test cannot see.
        await using var context = _fixture.CreateContext();
        var first = await SeedRelease(context, "1.0");
        var second = await SeedRelease(context, "2.0");

        first.Key.Should().NotBe(second.Key, "the database assigns each release its own key");

        // Act
        await using var reader = _fixture.CreateContext();
        var found = await reader.Releases
            .AsNoTracking()
            .Where(new IdOrKey(second.Key.ToString()).CreateFilter<Release>())
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken);

        // Assert
        found.Should().NotBeNull();
        found!.Id.Should().Be(second.Id);
        found.Version.Should().Be("2.0");
    }

    [Fact]
    public async Task Filter_ShouldStillFindTheReleaseByItsId()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var release = await SeedRelease(context, "3.0");

        // Act
        await using var reader = _fixture.CreateContext();
        var found = await reader.Releases
            .AsNoTracking()
            .Where(new IdOrKey(release.Id.ToString()).CreateFilter<Release>())
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken);

        // Assert
        found.Should().NotBeNull();
        found!.Id.Should().Be(release.Id);
    }

    [Fact]
    public async Task Projection_ShouldResolveTheProductThroughItsNavigation()
    {
        // Arrange — the projection reads Product through a navigation, which an in-memory fake leaves
        // null however the mapping is written. Only a real provider translates the join.
        _ = Mappings.Value;
        await using var context = _fixture.CreateContext();
        var release = await SeedRelease(context, "4.0");

        var productName = await context.Products
            .Where(p => p.Id == release.ProductId)
            .Select(p => p.Name)
            .FirstAsync(TestContext.Current.CancellationToken);

        // Act
        await using var reader = _fixture.CreateContext();
        var dto = await reader.Releases
            .AsNoTracking()
            .Where(r => r.Id == release.Id)
            .ProjectToType<ReleaseDto>()
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        // Assert
        dto.Should().NotBeNull();
        dto!.Product.Should().NotBeNull("a release always has the product it was cut against");
        dto.Product.Id.Should().Be(release.ProductId);
        dto.Product.Name.Should().Be(productName);
        dto.Version.Should().Be("4.0");
        dto.Status.Id.Should().Be(release.StatusId);
        dto.Status.Name.Should().Be(release.StatusName, "the status is flattened across four columns");
        dto.Status.Category.Should().Be(release.StatusCategory);
        dto.Status.Alias.Should().Be(release.StatusAliasValue);
        dto.Package.Should().BeNull("nothing writes PackageId, so no release reports a package");
    }
}
