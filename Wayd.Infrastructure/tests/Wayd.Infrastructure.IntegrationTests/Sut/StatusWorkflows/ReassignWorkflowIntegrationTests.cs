using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.StatusWorkflows.Commands;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Infrastructure.Persistence.Initialization;
using Wayd.Infrastructure.StatusWorkflows;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.Infrastructure.IntegrationTests.Sut.StatusWorkflows;

/// <summary>
/// Reassigns an owner type onto another workflow and proves every record came with it.
/// </summary>
/// <remarks>
/// The one path unit tests cannot cover. The handler loads workflows with <c>.Include(w =&gt; w.Statuses)</c>
/// and the migrator queries records by <c>StatusWorkflowId</c> — both are no-ops against an in-memory
/// fake, which happily returns fully-populated objects whatever the query asked for. Only a real
/// round-trip can tell a correct handler from one that forgot.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class ReassignWorkflowIntegrationTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    private static readonly Instant Timestamp = Instant.FromUtc(2026, 5, 4, 10, 0, 0);

    private static IDateTimeProvider DateTimeProvider()
    {
        var provider = new Mock<IDateTimeProvider>();
        provider.SetupGet(d => d.Now).Returns(Timestamp);
        provider.SetupGet(d => d.Today).Returns(new LocalDate(2026, 5, 4));

        return provider.Object;
    }

    private static ICurrentUser CurrentUser()
    {
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.GetUserId()).Returns(Guid.CreateVersion7().ToString());

        return user.Object;
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

    private async Task<Guid> SeedProductType(WaydDbContext context)
    {
        var existing = await context.ProductTypes
            .Select(t => t.Id)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        if (existing != Guid.Empty)
        {
            return existing;
        }

        await new ProductTypeSeeder().Initialize(context, DateTimeProvider(), TestContext.Current.CancellationToken);

        return await context.ProductTypes.Select(t => t.Id).FirstAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A published copy of the seeded workflow, so a remap between the two is legitimate.</summary>
    private static StatusWorkflow Replacement(StatusWorkflow source, string name)
    {
        var replacement = StatusWorkflow.Create(name, null, source.OwnerType).Value;

        foreach (var status in source.Statuses.OrderBy(s => s.Order))
        {
            replacement.AddStatus(status.Name, status.Description, status.Category, status.Alias);
        }

        replacement.Publish(EventActor.System, Timestamp);
        replacement.ClearDomainEvents();

        return replacement;
    }

    private ReassignWorkflowCommandHandler CreateSut(WaydDbContext context) =>
        new(
            context,
            [new ProductStatusRecordMigrator(context)],
            CurrentUser(),
            DateTimeProvider(),
            NullLogger<ReassignWorkflowCommandHandler>.Instance);

    [Fact]
    public async Task Reassign_ShouldMoveEveryRecordOntoTheTargetWorkflow()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var current = await ProductWorkflow(context);
        var productType = await SeedProductType(context);

        var replacement = Replacement(current, $"Replacement {Guid.CreateVersion7()}");
        context.StatusWorkflows.Add(replacement);

        // Its own scope, not the seeded organization-wide assignment: the whole collection shares one
        // database, so competing for that row makes these tests depend on each other's ordering.
        var assignment = WorkflowAssignment.Create(
            ProductWorkflowOwners.Product.Key,
            Guid.CreateVersion7(),
            current,
            EventActor.System,
            Timestamp).Value;

        assignment.ClearDomainEvents();
        context.WorkflowAssignments.Add(assignment);

        var products = Enumerable.Range(0, 3)
            .Select(i => Product.Create(
                $"Reassigned {i} {Guid.CreateVersion7()}",
                null,
                productType,
                null,
                null,
                StatusRef.From(current.Statuses.OrderBy(s => s.Order).First()),
                EventActor.System,
                Timestamp))
            .ToList();

        foreach (var product in products)
        {
            product.ClearDomainEvents();
            context.Products.Add(product);
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(context);

        // Act
        // No decisions supplied: the replacement carries the same names and aliases, so AutoMap
        // resolves every status on its own.
        var result = await sut.Handle(
            new ReassignWorkflowCommand(assignment.Id, replacement.Id, []),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // At least three: the migrator moves every product on the source workflow, and this shared
        // database holds products from other tests sitting on the same seeded workflow.
        result.Value.Should().BeGreaterThanOrEqualTo(3, "the three seeded products moved");

        await using var reader = _fixture.CreateContext();

        var reloaded = await reader.Products
            .Where(p => products.Select(x => x.Id).Contains(p.Id))
            .ToListAsync(TestContext.Current.CancellationToken);

        reloaded.Should().OnlyContain(p => p.StatusWorkflowId == replacement.Id);

        var reassigned = await reader.WorkflowAssignments
            .FirstAsync(a => a.Id == assignment.Id, TestContext.Current.CancellationToken);

        reassigned.WorkflowId.Should().Be(replacement.Id);
    }

    [Fact]
    public async Task Reassign_ShouldWriteATransitionForEveryMovedRecord()
    {
        // The migration is a status change like any other, so it belongs in the record's history —
        // otherwise a status appears to have changed with nothing explaining it.
        // Arrange
        await using var context = _fixture.CreateContext();
        var current = await ProductWorkflow(context);
        var productType = await SeedProductType(context);

        var replacement = Replacement(current, $"Replacement {Guid.CreateVersion7()}");
        context.StatusWorkflows.Add(replacement);

        // Its own scope, not the seeded organization-wide assignment: the whole collection shares one
        // database, so competing for that row makes these tests depend on each other's ordering.
        var assignment = WorkflowAssignment.Create(
            ProductWorkflowOwners.Product.Key,
            Guid.CreateVersion7(),
            current,
            EventActor.System,
            Timestamp).Value;

        assignment.ClearDomainEvents();
        context.WorkflowAssignments.Add(assignment);

        var product = Product.Create(
            $"Traced {Guid.CreateVersion7()}",
            null,
            productType,
            null,
            null,
            StatusRef.From(current.Statuses.OrderBy(s => s.Order).First()),
            EventActor.System,
            Timestamp);

        product.ClearDomainEvents();
        context.Products.Add(product);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var before = await context.StatusTransitions
            .CountAsync(t => t.RecordId == product.Id, TestContext.Current.CancellationToken);

        var sut = CreateSut(context);

        // Act
        var result = await sut.Handle(
            new ReassignWorkflowCommand(assignment.Id, replacement.Id, []),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await using var reader = _fixture.CreateContext();

        var after = await reader.StatusTransitions
            .CountAsync(t => t.RecordId == product.Id, TestContext.Current.CancellationToken);

        after.Should().Be(before + 1);
    }

    [Fact]
    public async Task Reassign_ShouldBeANoOp_WhenRunAgain()
    {
        // A migration that is interrupted must be safe to re-run. The second pass finds every record
        // already on the target, moves nothing, and writes no further history.
        // Arrange
        await using var context = _fixture.CreateContext();
        var current = await ProductWorkflow(context);
        var productType = await SeedProductType(context);

        var replacement = Replacement(current, $"Replacement {Guid.CreateVersion7()}");
        context.StatusWorkflows.Add(replacement);

        // Its own scope, not the seeded organization-wide assignment: the whole collection shares one
        // database, so competing for that row makes these tests depend on each other's ordering.
        var assignment = WorkflowAssignment.Create(
            ProductWorkflowOwners.Product.Key,
            Guid.CreateVersion7(),
            current,
            EventActor.System,
            Timestamp).Value;

        assignment.ClearDomainEvents();
        context.WorkflowAssignments.Add(assignment);

        var product = Product.Create(
            $"Repeated {Guid.CreateVersion7()}",
            null,
            productType,
            null,
            null,
            StatusRef.From(current.Statuses.OrderBy(s => s.Order).First()),
            EventActor.System,
            Timestamp);

        product.ClearDomainEvents();
        context.Products.Add(product);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(context);

        await sut.Handle(
            new ReassignWorkflowCommand(assignment.Id, replacement.Id, []),
            TestContext.Current.CancellationToken);

        var afterFirst = await context.StatusTransitions
            .CountAsync(t => t.RecordId == product.Id, TestContext.Current.CancellationToken);

        // Act
        // ReassignTo returns success without doing anything once the assignment already points at the
        // target, so the second run is expected to succeed having moved nothing.
        var second = await sut.Handle(
            new ReassignWorkflowCommand(assignment.Id, replacement.Id, []),
            TestContext.Current.CancellationToken);

        // Assert
        second.IsSuccess.Should().BeTrue();

        await using var reader = _fixture.CreateContext();

        var afterSecond = await reader.StatusTransitions
            .CountAsync(t => t.RecordId == product.Id, TestContext.Current.CancellationToken);

        afterSecond.Should().Be(afterFirst, "a record already on the target moves no further");
    }
}
