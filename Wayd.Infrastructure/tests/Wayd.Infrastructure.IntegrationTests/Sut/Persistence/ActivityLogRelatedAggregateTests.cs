using Mapster;
using Mapster.Utils;
using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Activities;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Infrastructure.Persistence.Initialization;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// An entry listed on the other records its event names, as well as on the record it was raised on.
/// </summary>
/// <remarks>
/// Only a real database shows the related rows being written and the reader's union translating: the unit
/// fakes neither map the owned table nor run the query the reader builds.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class ActivityLogRelatedAggregateTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    private static readonly Instant CreatedAt = Instant.FromUtc(2026, 5, 1, 8, 0, 0);
    private static readonly Instant MovedAt = Instant.FromUtc(2026, 5, 2, 9, 0, 0);

    /// <summary>
    /// Registers the Mapster mappings the reader projects through, as <c>ConfigureServices</c> does at
    /// startup. Without it a projection falls back to convention and silently drops configured members.
    /// </summary>
    private static readonly Lazy<bool> Mappings = new(() =>
    {
        var assembly = typeof(ActivityLogDto).Assembly;
        TypeAdapterConfig.GlobalSettings.Scan(assembly);
        TypeAdapterConfig.GlobalSettings.ScanInheritedTypes(assembly);
        return true;
    });

    [Fact]
    public async Task SaveChanges_WritesTheRelatedAggregatesAnEventNames()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (fromParent, toParent, child) = await SeedMove();

        // Act
        await using var verify = _fixture.CreateContext();
        var entry = await verify.ActivityLogs.AsNoTracking()
            .Include(a => a.RelatedAggregates)
            .SingleAsync(a => a.AggregateId == child.Id && a.EventType == nameof(ProductReparentedEventV2), ct);

        // Assert
        entry.RelatedAggregates.Should().BeEquivalentTo(
            [new AggregateReference("Product", fromParent.Id), new AggregateReference("Product", toParent.Id)]);
    }

    [Fact]
    public async Task Read_ListsAMoveOnBothParents_MarkedRelated_AndOnTheChild_AsItsOwn()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (fromParent, toParent, child) = await SeedMove();

        _ = Mappings.Value;
        await using var context = _fixture.CreateContext();
        var reader = new ActivityLogReader(context);

        // Act
        var onFromParent = await reader.Read(fromParent.Id, "Product", cancellationToken: ct);
        var onToParent = await reader.Read(toParent.Id, "Product", cancellationToken: ct);
        var onChild = await reader.Read(child.Id, "Product", cancellationToken: ct);

        // Assert
        foreach (var parentPage in new[] { onFromParent, onToParent })
        {
            parentPage.TotalCount.Should().Be(2, "each parent has its own creation and the move");
            parentPage.Items.Single(i => i.EventType == nameof(ProductReparentedEventV2))
                .Should().BeEquivalentTo(new { IsRelated = true, AggregateType = "Product", AggregateId = child.Id });
            parentPage.Items.Single(i => i.EventType == nameof(ProductAddedEvent)).IsRelated.Should().BeFalse();
        }

        onChild.Items.Should().OnlyContain(i => !i.IsRelated);
        onChild.Items.Select(i => i.EventType).Should().Contain(nameof(ProductReparentedEventV2));
    }

    [Fact]
    public async Task Read_MatchesARelatedAggregateOnItsTypeAsWellAsItsId()
    {
        // Arrange — one id named under a different type, which the typed read of a record must not pick up.
        var ct = TestContext.Current.CancellationToken;
        var sharedId = Guid.CreateVersion7();

        await using (var context = _fixture.CreateContext())
        {
            context.ActivityLogs.Add(Entry(Guid.CreateVersion7(), MovedAt, [new AggregateReference("Program", sharedId)]));
            await context.SaveChangesAsync(ct);
        }

        _ = Mappings.Value;
        await using var readContext = _fixture.CreateContext();
        var reader = new ActivityLogReader(readContext);

        // Act
        var asProgram = await reader.Read(sharedId, "Program", cancellationToken: ct);
        var asProject = await reader.Read(sharedId, "Project", cancellationToken: ct);
        var untyped = await reader.Read(sharedId, cancellationToken: ct);

        // Assert
        asProgram.Items.Should().ContainSingle().Which.IsRelated.Should().BeTrue();
        asProject.Items.Should().BeEmpty();
        untyped.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Read_WithTheTypeInAnotherCase_StillReadsTheRecordsOwnEntriesAsItsOwn()
    {
        // Arrange — the collation matches "project" to "Project", so the flag must agree with it.
        var ct = TestContext.Current.CancellationToken;
        var recordId = Guid.CreateVersion7();

        await using (var context = _fixture.CreateContext())
        {
            context.ActivityLogs.Add(Entry(recordId, MovedAt, []));
            await context.SaveChangesAsync(ct);
        }

        _ = Mappings.Value;
        await using var readContext = _fixture.CreateContext();
        var reader = new ActivityLogReader(readContext);

        // Act
        var page = await reader.Read(recordId, "project", cancellationToken: ct);

        // Assert
        page.Items.Should().ContainSingle().Which.IsRelated.Should().BeFalse();
    }

    [Fact]
    public async Task Read_PagesThroughOwnAndRelatedEntriesSharingOneTimestamp_WithoutRepeatingOrSkippingAny()
    {
        // Arrange — the union must not disturb the total order paging depends on.
        var ct = TestContext.Current.CancellationToken;
        var recordId = Guid.CreateVersion7();
        var related = new AggregateReference("Project", recordId);

        var written = new[]
        {
            Entry(recordId, MovedAt, [], ordinal: 0),
            Entry(Guid.CreateVersion7(), MovedAt, [related], ordinal: 1),
            Entry(recordId, MovedAt, [], ordinal: 2),
            Entry(Guid.CreateVersion7(), MovedAt, [related], ordinal: 3),
        };

        await using (var context = _fixture.CreateContext())
        {
            context.ActivityLogs.AddRange(written);
            await context.SaveChangesAsync(ct);
        }

        _ = Mappings.Value;
        await using var readContext = _fixture.CreateContext();
        var reader = new ActivityLogReader(readContext);

        // Act
        var paged = new List<ActivityLogDto>();
        for (var page = 1; page <= written.Length; page++)
        {
            var result = await reader.Read(recordId, "Project", page: page, pageSize: 1, cancellationToken: ct);
            result.TotalCount.Should().Be(written.Length);
            paged.Add(result.Items.Should().ContainSingle().Subject);
        }

        // Assert
        paged.Select(i => i.Id).Should().Equal(written.OrderByDescending(e => e.Ordinal).Select(e => e.Id));
        paged.Select(i => i.IsRelated).Should().Equal(true, false, true, false);
    }

    /// <summary>
    /// Creates two parents and a child under the first, then moves the child to the second in its own save.
    /// </summary>
    private async Task<(Product FromParent, Product ToParent, Product Child)> SeedMove()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();

        var initial = await InitialProductStatus(context);
        var productType = await SeedProductType(context);

        var fromParent = Product.Create($"From {Guid.CreateVersion7()}", null, productType, null, null, initial, EventActor.System, CreatedAt);
        var toParent = Product.Create($"To {Guid.CreateVersion7()}", null, productType, null, null, initial, EventActor.System, CreatedAt);
        context.Products.AddRange(fromParent, toParent);
        await context.SaveChangesAsync(ct);

        var child = Product.Create($"Child {Guid.CreateVersion7()}", null, productType, fromParent.Id, null, initial, EventActor.System, CreatedAt);
        context.Products.Add(child);
        await context.SaveChangesAsync(ct);

        child.Reparent(toParent.Id, [], false, EventActor.System, MovedAt).IsSuccess.Should().BeTrue();
        await context.SaveChangesAsync(ct);

        return (fromParent, toParent, child);
    }

    private static ActivityLogEntry Entry(Guid aggregateId, Instant timestamp, AggregateReference[] related, int ordinal = 0) =>
        new(Guid.CreateVersion7(), "ProjectStubEvent", ActivityCategory.Updated, "Ppm", "Project", aggregateId,
            EventActor.System, timestamp, ordinal, correlationId: null, "{}", "Project Stub", "1.0", related);

    private static IDateTimeProvider DateTimeProvider()
    {
        var provider = new Mock<IDateTimeProvider>();
        provider.SetupGet(d => d.Now).Returns(CreatedAt);
        provider.SetupGet(d => d.Today).Returns(new LocalDate(2026, 5, 1));

        return provider.Object;
    }

    private static async Task<StatusRef> InitialProductStatus(WaydDbContext context)
    {
        var ct = TestContext.Current.CancellationToken;
        ProductWorkflowOwners.Register();

        var workflow = await context.StatusWorkflows
            .Include(w => w.Statuses)
            .FirstOrDefaultAsync(w => w.OwnerType == ProductWorkflowOwners.Product.Key && w.IsSystem, ct);

        if (workflow is null)
        {
            await new ProductManagementWorkflowSeeder().Initialize(context, DateTimeProvider(), ct);

            workflow = await context.StatusWorkflows
                .Include(w => w.Statuses)
                .FirstAsync(w => w.OwnerType == ProductWorkflowOwners.Product.Key && w.IsSystem, ct);
        }

        return StatusRef.From(workflow.Statuses.OrderBy(s => s.Order).First());
    }

    private static async Task<Guid> SeedProductType(WaydDbContext context)
    {
        var ct = TestContext.Current.CancellationToken;

        var existing = await context.ProductTypes.Select(t => t.Id).FirstOrDefaultAsync(ct);
        if (existing != Guid.Empty)
        {
            return existing;
        }

        await new ProductTypeSeeder().Initialize(context, DateTimeProvider(), ct);

        return await context.ProductTypes.Select(t => t.Id).FirstAsync(ct);
    }
}
