using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.ProductManagement.Application;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.Releases.Commands;
using Wayd.ProductManagement.Application.Releases.Queries;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// How a release list orders itself, against SQL Server.
/// </summary>
/// <remarks>
/// Sequence orders one product's releases against each other. It means nothing across products —
/// 4.8.2 of one has no position relative to 2026.04 of another beyond the date they shipped — so it
/// participates only when the list is scoped to a product. Both halves of that need a real provider:
/// an in-memory fake sorts in memory and would agree with any ordering the query asked for.
/// </remarks>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class ReleaseOrderingTests(WaydSqlServerApiFactory factory)
{
    private readonly WaydSqlServerApiFactory _factory = factory;

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}"[..24];

    private static async Task<Guid> SeedProduct(
        IDispatcher dispatcher, IProductManagementDbContext dbContext)
    {
        var productTypeId = await dbContext.ProductTypes
            .Where(t => t.IsActive && t.IsReleasable)
            .Select(t => t.Id)
            .FirstAsync(TestContext.Current.CancellationToken);

        var product = await dispatcher.Send(
            new CreateProductCommand(Unique("Node"), null, productTypeId, null, null),
            TestContext.Current.CancellationToken);
        Assert.True(product.IsSuccess, product.IsFailure ? product.Error : null);

        return product.Value.Id;
    }

    private static async Task<Guid> SeedRelease(
        IDispatcher dispatcher, Guid productId, string version, long? sequence, LocalDate? released)
    {
        var planned = await dispatcher.Send(
            new PlanReleaseCommand(productId, version, null, null, sequence),
            TestContext.Current.CancellationToken);
        Assert.True(planned.IsSuccess, planned.IsFailure ? planned.Error : null);

        if (released is not null)
        {
            var marked = await dispatcher.Send(
                new MarkReleaseReleasedCommand(planned.Value.Id, released.Value),
                TestContext.Current.CancellationToken);
            Assert.True(marked.IsSuccess, marked.IsFailure ? marked.Error : null);
        }

        return planned.Value.Id;
    }

    [Fact]
    public async Task GetReleases_ForOneProduct_OrdersBySequenceWithinTheSameReleasedDate()
    {
        // Arrange — two releases of one product shipped the same day, so the date cannot separate them
        // and only the sequence can.
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IProductManagementDbContext>();

        var productId = await SeedProduct(dispatcher, dbContext);
        var shipped = new LocalDate(2026, 4, 20);

        var lower = await SeedRelease(dispatcher, productId, "4.8.1", 10, shipped);
        var higher = await SeedRelease(dispatcher, productId, "4.8.2", 20, shipped);

        // Act
        var releases = await dispatcher.Send(
            new GetReleasesQuery(productId), TestContext.Current.CancellationToken);

        // Assert — descending, so the higher sequence leads.
        Assert.Equal([higher, lower], releases.Select(r => r.Id));
    }

    [Fact]
    public async Task GetReleases_AcrossProducts_IgnoresSequence()
    {
        // Arrange — one release per product, shipped the same day, sequenced so that honouring the
        // sequence would invert the order a date-only sort produces. A sequence set to order one
        // product's releases must not move another product's release that happens to share a date.
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IProductManagementDbContext>();

        var firstProduct = await SeedProduct(dispatcher, dbContext);
        var secondProduct = await SeedProduct(dispatcher, dbContext);
        var shipped = new LocalDate(2026, 4, 21);

        var lowSequence = await SeedRelease(dispatcher, firstProduct, "1.0", 10, shipped);
        var highSequence = await SeedRelease(dispatcher, secondProduct, "2026.04", 99, shipped);

        // Act
        var releases = await dispatcher.Send(
            new GetReleasesQuery(), TestContext.Current.CancellationToken);

        // Assert — both are present and neither sequence has pulled one above the other on the basis
        // of a number that only means something within its own product.
        var ours = releases.Where(r => r.Id == lowSequence || r.Id == highSequence).ToList();
        Assert.Equal(2, ours.Count);
        Assert.All(ours, r => Assert.Equal(shipped, r.ReleasedDate));
    }

    [Fact]
    public async Task GetReleases_PutsUnreleasedFirst()
    {
        // Arrange — a planned release has no date to sort on, and belongs at the top rather than the
        // bottom: what is coming matters more than what already shipped.
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IProductManagementDbContext>();

        var productId = await SeedProduct(dispatcher, dbContext);

        var shippedRelease = await SeedRelease(
            dispatcher, productId, "1.0", null, new LocalDate(2026, 4, 20));
        var plannedRelease = await SeedRelease(dispatcher, productId, "2.0", null, null);

        // Act
        var releases = await dispatcher.Send(
            new GetReleasesQuery(productId), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([plannedRelease, shippedRelease], releases.Select(r => r.Id));
    }

    [Fact]
    public async Task GetReleases_TracksNothing_BecauseItProjects()
    {
        // Arrange — the query drops AsNoTracking, which reads like an oversight. It is not: a query
        // that projects to a DTO before materializing never produces an entity for the change tracker
        // to hold, so AsNoTracking would be a no-op. Asserted against SQL Server because only a real
        // provider builds a real change tracker.
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IProductManagementDbContext>();
        var trackingContext = scope.ServiceProvider.GetRequiredService<WaydDbContext>();

        var productId = await SeedProduct(dispatcher, dbContext);
        await SeedRelease(dispatcher, productId, "3.0", null, new LocalDate(2026, 4, 22));

        // The seeding above tracked entities of its own; only what the query adds is in question.
        trackingContext.ChangeTracker.Clear();

        // Act
        var releases = await dispatcher.Send(
            new GetReleasesQuery(productId), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(releases);
        Assert.Empty(trackingContext.ChangeTracker.Entries());
    }
}
