using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.StatusWorkflows.Commands;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Common.Models;
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
/// <para>
/// Every test reassigns the organization-wide product assignment, because that is the only scope the
/// migrator accepts. The collection shares one database, so a test cannot assume which workflow the
/// assignment points at when it starts: each takes whatever it finds as the workflow being moved from,
/// and builds its records and its replacement from that. A run therefore leaves the assignment on the
/// last replacement created, not the seeded system workflow, and every product on the workflow it
/// started from — other classes' products included — carries one more transition. A test elsewhere in
/// the collection must find the system workflow by <c>IsSystem</c>, not through the assignment.
/// </para>
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

    private static ICurrentPrincipal CurrentPrincipal(Guid? employeeId = null)
    {
        var principal = new Mock<ICurrentPrincipal>();
        principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(employeeId);

        return principal.Object;
    }

    /// <summary>
    /// The organization-wide product assignment and the workflow it currently points at, seeding the
    /// defaults on an empty database.
    /// </summary>
    private async Task<(WorkflowAssignment Assignment, StatusWorkflow Current)> OrganizationAssignment(WaydDbContext context)
    {
        ProductWorkflowOwners.Register();

        var assignment = await context.WorkflowAssignments
            .FirstOrDefaultAsync(
                a => a.OwnerType == ProductWorkflowOwners.Product.Key && a.ScopeId == null,
                TestContext.Current.CancellationToken);

        if (assignment is null)
        {
            await new ProductManagementWorkflowSeeder()
                .Initialize(context, DateTimeProvider(), TestContext.Current.CancellationToken);

            assignment = await context.WorkflowAssignments
                .FirstAsync(
                    a => a.OwnerType == ProductWorkflowOwners.Product.Key && a.ScopeId == null,
                    TestContext.Current.CancellationToken);
        }

        var current = await context.StatusWorkflows
            .Include(w => w.Statuses)
            .FirstAsync(w => w.Id == assignment.WorkflowId, TestContext.Current.CancellationToken);

        return (assignment, current);
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

    private static async Task<Employee> SeedEmployee(WaydDbContext context)
    {
        var employee = Employee.Create(
            new PersonName("Avery", null, "Chen"),
            $"{Guid.CreateVersion7():N}"[..12],
            Timestamp,
            new EmailAddress($"{Guid.CreateVersion7():N}"[..12] + "@acme.example"),
            jobTitle: null,
            department: null,
            officeLocation: null,
            managerId: null,
            isActive: true,
            employeeType: null,
            EventActor.System,
            Timestamp);

        context.Employees.Add(employee);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return employee;
    }

    /// <summary>A published copy of the workflow in use, so a remap between the two is legitimate.</summary>
    private static StatusWorkflow Replacement(StatusWorkflow source)
    {
        var replacement = StatusWorkflow.Create(
            $"Replacement {Guid.CreateVersion7()}", null, source.OwnerType, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;

        foreach (var status in source.Statuses.OrderBy(s => s.Order))
        {
            replacement.AddStatus(status.Name, status.Description, status.Category, status.Alias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));
        }

        replacement.Publish(EventActor.System, Timestamp);
        replacement.ClearDomainEvents();

        return replacement;
    }

    /// <summary>
    /// A published copy of the workflow in use, restricted to moving each status to the next in order — so
    /// no status on the workflow being moved from is among those any move is allowed from.
    /// </summary>
    private static StatusWorkflow RestrictedReplacement(StatusWorkflow source)
    {
        var replacement = StatusWorkflow.Create(
            $"Restricted Replacement {Guid.CreateVersion7()}", null, source.OwnerType, EventActor.System, Timestamp).Value;

        var statuses = source.Statuses
            .OrderBy(s => s.Order)
            .Select(s => replacement.AddStatus(s.Name, s.Description, s.Category, s.Alias, EventActor.System, Timestamp).Value)
            .ToList();

        // Each status moves to the next; a Proposed or Active last status loops back so none is a dead end.
        var moves = statuses.Zip(statuses.Skip(1), (from, to) => (from.Id, to.Id)).ToList();
        if (statuses[^1].Category is StatusCategory.Proposed or StatusCategory.Active)
        {
            moves.Add((statuses[^1].Id, statuses[0].Id));
        }

        replacement.SetTransitionMode(TransitionMode.Restricted, EventActor.System, Timestamp);
        replacement.SetTransitions(moves, EventActor.System, Timestamp);
        replacement.Publish(EventActor.System, Timestamp).IsSuccess.Should().BeTrue();
        replacement.ClearDomainEvents();

        return replacement;
    }

    private static Product SeedProduct(WaydDbContext context, StatusWorkflow workflow, Guid productTypeId, string name)
    {
        var product = Product.Create(
            $"{name} {Guid.CreateVersion7()}",
            null,
            productTypeId,
            null,
            null,
            StatusRef.From(workflow.Statuses.OrderBy(s => s.Order).First()),
            EventActor.System,
            Timestamp);

        product.ClearDomainEvents();
        context.Products.Add(product);

        return product;
    }

    private static ReassignWorkflowCommandHandler CreateSut(WaydDbContext context, Guid? employeeId = null) =>
        new(
            context,
            [new ProductStatusRecordMigrator(context)],
            CurrentUser(),
            CurrentPrincipal(employeeId),
            DateTimeProvider(),
            NullLogger<ReassignWorkflowCommandHandler>.Instance);

    [Fact]
    public async Task Reassign_ShouldMoveEveryRecordOntoTheTargetWorkflow()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var (assignment, current) = await OrganizationAssignment(context);
        var productType = await SeedProductType(context);

        var replacement = Replacement(current);
        context.StatusWorkflows.Add(replacement);

        var products = Enumerable.Range(0, 3)
            .Select(i => SeedProduct(context, current, productType, $"Reassigned {i}"))
            .ToList();

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
        // database holds products from other tests sitting on the same workflow.
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
    public async Task Reassign_ShouldMoveEveryRecord_WhateverMovesTheTargetWorkflowAllows()
    {
        // A migration must never be blocked by the target workflow's transitions: no status on the old
        // workflow is one the new one allows a move from, yet every record must come across.
        // Arrange
        await using var context = _fixture.CreateContext();
        var (assignment, current) = await OrganizationAssignment(context);
        var productType = await SeedProductType(context);

        var restricted = RestrictedReplacement(current);
        context.StatusWorkflows.Add(restricted);

        var products = Enumerable.Range(0, 2)
            .Select(i => SeedProduct(context, current, productType, $"Gated {i}"))
            .ToList();

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(context);

        try
        {
            // Act
            var result = await sut.Handle(
                new ReassignWorkflowCommand(assignment.Id, restricted.Id, []),
                TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

            await using var reader = _fixture.CreateContext();

            var reloaded = await reader.Products
                .Where(p => products.Select(x => x.Id).Contains(p.Id))
                .ToListAsync(TestContext.Current.CancellationToken);

            reloaded.Should().HaveCount(2).And.OnlyContain(p => p.StatusWorkflowId == restricted.Id);
        }
        finally
        {
            // Hand the organization-wide assignment on to an any-to-any workflow, so no later test in the
            // collection finds products governed by restricted transitions.
            await using var cleanup = _fixture.CreateContext();
            var (_, now) = await OrganizationAssignment(cleanup);
            var open = Replacement(now);
            cleanup.StatusWorkflows.Add(open);
            await cleanup.SaveChangesAsync(TestContext.Current.CancellationToken);

            await CreateSut(cleanup).Handle(
                new ReassignWorkflowCommand(assignment.Id, open.Id, []),
                TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Reassign_ShouldWriteATransitionForEveryMovedRecord()
    {
        // The migration is a status change like any other, so it belongs in the record's history —
        // otherwise a status appears to have changed with nothing explaining it.
        // Arrange
        await using var context = _fixture.CreateContext();
        var (assignment, current) = await OrganizationAssignment(context);
        var productType = await SeedProductType(context);

        var replacement = Replacement(current);
        context.StatusWorkflows.Add(replacement);

        var product = SeedProduct(context, current, productType, "Traced");
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
    public async Task Reassign_ShouldAttributeTheMigrationTransitionsToTheActingEmployee()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var (assignment, current) = await OrganizationAssignment(context);
        var productType = await SeedProductType(context);
        var employee = await SeedEmployee(context);

        var replacement = Replacement(current);
        context.StatusWorkflows.Add(replacement);

        var product = SeedProduct(context, current, productType, "Attributed");
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(context, employee.Id);

        // Act
        var result = await sut.Handle(
            new ReassignWorkflowCommand(assignment.Id, replacement.Id, []),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await using var reader = _fixture.CreateContext();

        var migration = await reader.StatusTransitions
            .Where(t => t.RecordId == product.Id && t.WorkflowId == replacement.Id)
            .SingleAsync(TestContext.Current.CancellationToken);

        migration.ActorEmployeeId.Should().Be(employee.Id);
    }

    [Fact]
    public async Task Reassign_ShouldMigrate_WhenTheAccountHasNoLinkedEmployee()
    {
        // An administrator need not be an employee, and reassigning must not depend on being one.
        // Arrange
        await using var context = _fixture.CreateContext();
        var (assignment, current) = await OrganizationAssignment(context);
        var productType = await SeedProductType(context);

        var replacement = Replacement(current);
        context.StatusWorkflows.Add(replacement);

        var product = SeedProduct(context, current, productType, "Unlinked");
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(context, employeeId: null);

        // Act
        var result = await sut.Handle(
            new ReassignWorkflowCommand(assignment.Id, replacement.Id, []),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await using var reader = _fixture.CreateContext();

        var migration = await reader.StatusTransitions
            .Where(t => t.RecordId == product.Id && t.WorkflowId == replacement.Id)
            .SingleAsync(TestContext.Current.CancellationToken);

        migration.ActorEmployeeId.Should().BeNull();
        migration.ActorUserId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Reassign_ShouldRefuseAScopedAssignment()
    {
        // Product records carry no scope, so a scoped migration could only select by workflow — moving
        // every product on it, not just that scope's. Refusing leaves everything where it was.
        // Arrange
        await using var context = _fixture.CreateContext();
        var (_, current) = await OrganizationAssignment(context);
        var productType = await SeedProductType(context);

        var replacement = Replacement(current);
        context.StatusWorkflows.Add(replacement);

        var scoped = WorkflowAssignment.Create(
            ProductWorkflowOwners.Product.Key,
            Guid.CreateVersion7(),
            current,
            EventActor.System,
            Timestamp).Value;

        scoped.ClearDomainEvents();
        context.WorkflowAssignments.Add(scoped);

        var product = SeedProduct(context, current, productType, "Unscoped");
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(context);

        try
        {
            // Act
            var result = await sut.Handle(
                new ReassignWorkflowCommand(scoped.Id, replacement.Id, []),
                TestContext.Current.CancellationToken);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Should().Contain("organization-wide");

            await using var reader = _fixture.CreateContext();

            var reloaded = await reader.Products.SingleAsync(p => p.Id == product.Id, TestContext.Current.CancellationToken);
            reloaded.StatusWorkflowId.Should().Be(current.Id);

            var unchanged = await reader.WorkflowAssignments.SingleAsync(a => a.Id == scoped.Id, TestContext.Current.CancellationToken);
            unchanged.WorkflowId.Should().Be(current.Id);
        }
        finally
        {
            // Left behind, the scoped row would keep pointing at the workflow the organization-wide
            // assignment later moves off, and stop that workflow from ever being archived.
            await using var cleanup = _fixture.CreateContext();
            await cleanup.WorkflowAssignments
                .Where(a => a.Id == scoped.Id)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Reassign_ShouldBeANoOp_WhenRunAgain()
    {
        // A migration that is interrupted must be safe to re-run. The second pass finds every record
        // already on the target, moves nothing, and writes no further history.
        // Arrange
        await using var context = _fixture.CreateContext();
        var (assignment, current) = await OrganizationAssignment(context);
        var productType = await SeedProductType(context);

        var replacement = Replacement(current);
        context.StatusWorkflows.Add(replacement);

        var product = SeedProduct(context, current, productType, "Repeated");
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
