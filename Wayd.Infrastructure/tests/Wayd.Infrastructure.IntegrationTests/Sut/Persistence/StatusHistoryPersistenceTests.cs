using Microsoft.EntityFrameworkCore;
using NodaTime;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Infrastructure.Persistence.Initialization;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Round-trips a status-tracked aggregate through SQL Server to prove the transition log is actually
/// written.
/// </summary>
/// <remarks>
/// The history was mapped with <c>Ignore</c> at first, so <c>ApplyStatus</c> appended to a list EF never
/// read and every transition was dropped on save. Nothing caught it: the domain tests assert against the
/// same in-memory list and pass whether or not the row reaches the database. Only a round-trip can tell
/// the difference, which is what these do.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class StatusHistoryPersistenceTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    private static readonly Instant Timestamp = Instant.FromUtc(2026, 3, 4, 10, 0, 0);

    private static IDateTimeProvider DateTimeProvider()
    {
        var provider = new Mock<IDateTimeProvider>();
        provider.SetupGet(d => d.Now).Returns(Timestamp);
        provider.SetupGet(d => d.Today).Returns(new LocalDate(2026, 3, 4));

        return provider.Object;
    }

    private static StatusRef StatusOf(StatusWorkflow workflow, int index)
    {
        var status = workflow.Statuses.OrderBy(s => s.Order).Skip(index).First();

        return StatusRef.From(status);
    }

    private async Task<StatusWorkflow> ProductWorkflow(WaydDbContext context)
    {
        ProductWorkflowOwners.Register();

        var workflow = await context.StatusWorkflows
            .Include(w => w.Statuses)
            .FirstOrDefaultAsync(
                w => w.OwnerType == ProductWorkflowOwners.Product.Key && w.IsSystem,
                TestContext.Current.CancellationToken);

        if (workflow is null)
        {
            await new ProductManagementWorkflowSeeder()
                .Initialize(context, DateTimeProvider(), TestContext.Current.CancellationToken);

            workflow = await context.StatusWorkflows
                .Include(w => w.Statuses)
                .FirstAsync(
                    w => w.OwnerType == ProductWorkflowOwners.Product.Key && w.IsSystem,
                    TestContext.Current.CancellationToken);
        }

        return workflow;
    }

    [Fact]
    public async Task Create_ShouldPersistTheOpeningTransition()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var workflow = await ProductWorkflow(context);
        var productType = await SeedProductType(context);

        var product = Product.Create(
            $"Round trip {Guid.CreateVersion7()}",
            null,
            productType,
            null,
            null,
            StatusOf(workflow, 0),
            EventActor.System,
            Timestamp);

        // Act
        context.Products.Add(product);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var transitions = await context.StatusTransitions
            .Where(t => t.RecordId == product.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        transitions.Should().ContainSingle();
        transitions[0].OwnerType.Should().Be(ProductWorkflowOwners.Product.Key);
        transitions[0].FromStatusId.Should().BeNull("the opening transition has nothing to come from");
        transitions[0].Sequence.Should().Be(0);
    }

    [Fact]
    public async Task ChangeStatus_ShouldAppendATransition()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var workflow = await ProductWorkflow(context);
        var productType = await SeedProductType(context);

        var product = Product.Create(
            $"Round trip {Guid.CreateVersion7()}",
            null,
            productType,
            null,
            null,
            StatusOf(workflow, 0),
            EventActor.System,
            Timestamp);

        context.Products.Add(product);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var target = StatusOf(workflow, 1);
        product.ChangeStatus(target, EventActor.System, Timestamp.Plus(Duration.FromHours(1)));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var transitions = await context.StatusTransitions
            .Where(t => t.RecordId == product.Id)
            .OrderBy(t => t.Sequence)
            .ToListAsync(TestContext.Current.CancellationToken);

        transitions.Should().HaveCount(2);
        transitions[1].ToStatusId.Should().Be(target.StatusId);
        transitions[1].ToStatusName.Should().Be(target.Name);
        transitions[1].Sequence.Should().Be(1);
    }

    [Fact]
    public async Task Reload_ShouldFindTheHistoryInItsOwnTable()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var workflow = await ProductWorkflow(context);
        var productType = await SeedProductType(context);

        var product = Product.Create(
            $"Round trip {Guid.CreateVersion7()}",
            null,
            productType,
            null,
            null,
            StatusOf(workflow, 0),
            EventActor.System,
            Timestamp);

        context.Products.Add(product);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await using var reader = _fixture.CreateContext();
        var reloaded = await reader.Products
            .FirstAsync(p => p.Id == product.Id, TestContext.Current.CancellationToken);

        var history = await reader.StatusTransitions
            .Where(t => t.OwnerType == ProductWorkflowOwners.Product.Key && t.RecordId == product.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        // The history is not a navigation, so a reloaded aggregate carries none of it — the count on the
        // record is what tells a reader there is history to fetch.
        reloaded.StatusTransitions.Should().BeEmpty();
        reloaded.StatusTransitionCount.Should().Be(1);
        history.Should().ContainSingle();
    }

    [Fact]
    public async Task StatusWorkflowId_ShouldSurviveAReload()
    {
        // The workflow a record is on used to be derived from its newest status transition. The history
        // is not a navigation and DrainStatusTransitions empties the in-memory list on every save, so a
        // reloaded record reported no workflow at all — which is what made a batched migration
        // unresumable. Only a round-trip can tell the difference.
        // Arrange
        await using var context = _fixture.CreateContext();
        var workflow = await ProductWorkflow(context);
        var productType = await SeedProductType(context);

        var product = Product.Create(
            $"Round trip {Guid.CreateVersion7()}",
            null,
            productType,
            null,
            null,
            StatusOf(workflow, 0),
            EventActor.System,
            Timestamp);

        context.Products.Add(product);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await using var reader = _fixture.CreateContext();
        var reloaded = await reader.Products
            .FirstAsync(p => p.Id == product.Id, TestContext.Current.CancellationToken);

        // Assert
        reloaded.StatusTransitions.Should().BeEmpty("the history is not a navigation");
        reloaded.StatusWorkflowId.Should().Be(workflow.Id);
    }

    [Fact]
    public async Task SwitchWorkflow_ShouldBeANoOp_ForAReloadedRecordAlreadyMoved()
    {
        // The resumability guarantee, against a real round-trip: a batched migration that is
        // interrupted and re-run must skip records it already moved rather than fail on them. The
        // in-memory domain test cannot show this — it never saves, so it never drains the transitions
        // the old code derived the current workflow from.
        // Arrange
        await using var context = _fixture.CreateContext();
        var workflow = await ProductWorkflow(context);
        var productType = await SeedProductType(context);

        var replacement = StatusWorkflow.Create(
            $"Replacement {Guid.CreateVersion7()}", null, ProductWorkflowOwners.Product.Key).Value;

        foreach (var status in workflow.Statuses.OrderBy(x => x.Order))
        {
            replacement.AddStatus(status.Name, null, status.Category, status.Alias);
        }

        replacement.Publish(EventActor.System, Timestamp);
        context.StatusWorkflows.Add(replacement);

        var product = Product.Create(
            $"Resumable {Guid.CreateVersion7()}",
            null,
            productType,
            null,
            null,
            StatusOf(workflow, 0),
            EventActor.System,
            Timestamp);

        context.Products.Add(product);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var remap = StatusRemap.AutoMap(workflow, replacement).Value;

        // The first pass of the migration, then the save that drains the in-memory history.
        product.SwitchWorkflow(remap, EventActor.System, Timestamp).IsSuccess.Should().BeTrue();
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        // The re-run, against the record as a fresh batch would load it.
        await using var reader = _fixture.CreateContext();
        var reloaded = await reader.Products
            .FirstAsync(p => p.Id == product.Id, TestContext.Current.CancellationToken);

        var before = reloaded.StatusTransitionCount;
        var result = reloaded.SwitchWorkflow(remap, EventActor.System, Timestamp);

        // Assert
        reloaded.StatusWorkflowId.Should().Be(replacement.Id, "the first pass moved it");
        result.IsSuccess.Should().BeTrue("re-running over an already-moved record is a no-op");
        reloaded.StatusTransitionCount.Should().Be(before, "a no-op records no transition");
    }

    private async Task<Guid> SeedProductType(WaydDbContext context)
    {
        var existing = await context.ProductTypes
            .Select(t => t.Id)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        if (existing != Guid.Empty)
        {
            return existing;
        }

        await new ProductTypeSeeder()
            .Initialize(context, DateTimeProvider(), TestContext.Current.CancellationToken);

        return await context.ProductTypes
            .Select(t => t.Id)
            .FirstAsync(TestContext.Current.CancellationToken);
    }
}
