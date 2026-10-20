using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Commands;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.ProductManagement.Domain;

namespace Wayd.Infrastructure.IntegrationTests.Sut.StatusWorkflows;

/// <summary>
/// A workflow's transitions through a real save: persisted with their mode, loaded by every handler and
/// the resolver that needs them, removed with their status, and recorded in the activity log.
/// </summary>
/// <remarks>
/// Each handler here loads transitions with <c>.Include(w =&gt; w.Transitions)</c>, a no-op against the
/// in-memory fakes, so only a real round-trip shows the collection actually arrives. Every workflow is
/// created here and none is assigned organization-wide, so the shared database's product assignment is
/// left alone.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class WorkflowTransitionIntegrationTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 5, 4, 10, 0, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    private static IDateTimeProvider DateTimeProvider()
    {
        var provider = new Mock<IDateTimeProvider>();
        provider.SetupGet(d => d.Now).Returns(Now);

        return provider.Object;
    }

    private static ICurrentUser CurrentUser()
    {
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.GetUserId()).Returns(Guid.CreateVersion7().ToString());

        return user.Object;
    }

    /// <summary>
    /// A saved draft product workflow restricted to Concept → Active → Sunset → Retired.
    /// </summary>
    private static async Task<StatusWorkflow> SeedRestrictedWorkflow(WaydDbContext context)
    {
        ProductWorkflowOwners.Register();

        var workflow = StatusWorkflow.Create($"Gated Lifecycle {Guid.CreateVersion7()}", null, ProductWorkflowOwners.Product.Key, EventActor.System, Now).Value;
        var concept = workflow.AddStatus("Concept", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, Now).Value;
        var active = workflow.AddStatus("Active", null, StatusCategory.Active, (int)ProductStatusAlias.Active, EventActor.System, Now).Value;
        var sunset = workflow.AddStatus("Sunset", null, StatusCategory.Active, (int)ProductStatusAlias.Sunset, EventActor.System, Now).Value;
        var retired = workflow.AddStatus("Retired", null, StatusCategory.Done, (int)ProductStatusAlias.Retired, EventActor.System, Now).Value;
        workflow.SetTransitionMode(TransitionMode.Restricted, EventActor.System, Now);
        workflow.SetTransitions([(concept.Id, active.Id), (active.Id, sunset.Id), (sunset.Id, retired.Id)], EventActor.System, Now);

        context.StatusWorkflows.Add(workflow);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return workflow;
    }

    private static Guid StatusId(StatusWorkflow workflow, string name) => workflow.Statuses.Single(s => s.Name == name).Id;

    private static IEnumerable<(string From, string To)> Moves(StatusWorkflow workflow) =>
        workflow.Transitions.Select(t => (
            workflow.Statuses.Single(s => s.Id == t.FromStatusId).Name,
            workflow.Statuses.Single(s => s.Id == t.ToStatusId).Name));

    private async Task<StatusWorkflow> Reload(Guid workflowId)
    {
        await using var reader = _fixture.CreateContext();

        return await reader.StatusWorkflows
            .AsNoTracking()
            .Include(w => w.Statuses)
            .Include(w => w.Transitions)
            .SingleAsync(w => w.Id == workflowId, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SaveChanges_ShouldPersistTheModeAndTransitions()
    {
        // Arrange
        await using var context = _fixture.CreateContext();

        // Act
        var workflow = await SeedRestrictedWorkflow(context);

        // Assert
        var reloaded = await Reload(workflow.Id);
        reloaded.TransitionMode.Should().Be(TransitionMode.Restricted);
        Moves(reloaded).Should().BeEquivalentTo([("Concept", "Active"), ("Active", "Sunset"), ("Sunset", "Retired")]);
        reloaded.Transitions.Select(t => t.Id).Should().BeEquivalentTo(workflow.Transitions.Select(t => t.Id));
    }

    [Fact]
    public async Task SaveChanges_ShouldRecordTheModeAndTransitionChangesInTheActivityLog()
    {
        // Arrange
        await using var context = _fixture.CreateContext();

        // Act
        var workflow = await SeedRestrictedWorkflow(context);

        // Assert
        var entries = await context.ActivityLogs.AsNoTracking()
            .Where(a => a.AggregateId == workflow.Id)
            .OrderBy(a => a.Timestamp).ThenBy(a => a.Ordinal)
            .Select(a => a.EventType)
            .ToListAsync(TestContext.Current.CancellationToken);

        entries.Should().Contain([nameof(WorkflowTransitionModeChangedEvent), nameof(WorkflowTransitionsChangedEvent)]);
    }

    [Fact]
    public async Task SetTransitions_ShouldReplaceTheStoredSet_KeepingKeptRows()
    {
        // Arrange
        StatusWorkflow workflow;
        await using (var setup = _fixture.CreateContext())
        {
            workflow = await SeedRestrictedWorkflow(setup);
        }

        var kept = workflow.Transitions.Single(t => t.FromStatusId == StatusId(workflow, "Concept"));

        await using var context = _fixture.CreateContext();

        var sut = new SetWorkflowTransitionsCommandHandler(
            context, CurrentUser(), DateTimeProvider(), NullLogger<SetWorkflowTransitionsCommandHandler>.Instance);

        // Act
        var result = await sut.Handle(
            new SetWorkflowTransitionsCommand(workflow.Id,
            [
                new(StatusId(workflow, "Concept"), StatusId(workflow, "Active")),
                new(StatusId(workflow, "Active"), StatusId(workflow, "Retired")),
            ]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

        var reloaded = await Reload(workflow.Id);
        Moves(reloaded).Should().BeEquivalentTo([("Concept", "Active"), ("Active", "Retired")]);
        reloaded.Transitions.Should().Contain(t => t.Id == kept.Id);
    }

    [Fact]
    public async Task RemoveStatus_ShouldDeleteTheTransitionsThatTouchedIt()
    {
        // Arrange
        Guid workflowId;
        Guid sunsetId;
        await using (var setup = _fixture.CreateContext())
        {
            var seeded = await SeedRestrictedWorkflow(setup);
            (workflowId, sunsetId) = (seeded.Id, StatusId(seeded, "Sunset"));
        }

        await using var context = _fixture.CreateContext();
        var sut = new RemoveWorkflowStatusCommandHandler(
            context, CurrentUser(), DateTimeProvider(), NullLogger<RemoveWorkflowStatusCommandHandler>.Instance);

        // Act
        var result = await sut.Handle(new RemoveWorkflowStatusCommand(workflowId, sunsetId), TestContext.Current.CancellationToken);

        // Assert
        // Without the transitions loaded, the delete of a status they still reference fails on the
        // foreign key.
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

        var reloaded = await Reload(workflowId);
        reloaded.Statuses.Select(s => s.Name).Should().Equal("Concept", "Active", "Retired");
        Moves(reloaded).Should().BeEquivalentTo([("Concept", "Active")]);
    }

    [Fact]
    public async Task Clone_ShouldCopyTheTransitionsOntoTheClone()
    {
        // Arrange
        Guid sourceId;
        await using (var setup = _fixture.CreateContext())
        {
            sourceId = (await SeedRestrictedWorkflow(setup)).Id;
        }

        await using var context = _fixture.CreateContext();
        var sut = new CloneStatusWorkflowCommandHandler(
            context, CurrentUser(), DateTimeProvider(), NullLogger<CloneStatusWorkflowCommandHandler>.Instance);

        // Act
        var result = await sut.Handle(
            new CloneStatusWorkflowCommand(sourceId, $"Gated Lifecycle Copy {Guid.CreateVersion7()}", null), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

        var clone = await Reload(result.Value);
        clone.TransitionMode.Should().Be(TransitionMode.Restricted);
        Moves(clone).Should().BeEquivalentTo([("Concept", "Active"), ("Active", "Sunset"), ("Sunset", "Retired")]);
    }

    [Fact]
    public async Task Publish_ShouldSeeTheStoredTransitions()
    {
        // Arrange
        Guid workflowId;
        await using (var setup = _fixture.CreateContext())
        {
            workflowId = (await SeedRestrictedWorkflow(setup)).Id;
        }

        await using var context = _fixture.CreateContext();
        var sut = new PublishStatusWorkflowCommandHandler(
            context, CurrentUser(), DateTimeProvider(), NullLogger<PublishStatusWorkflowCommandHandler>.Instance);

        // Act
        var result = await sut.Handle(new PublishStatusWorkflowCommand(workflowId), TestContext.Current.CancellationToken);

        // Assert
        // Unloaded, the transitions read as none, and every status but the initial one as unreachable.
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        (await Reload(workflowId)).State.Should().Be(StatusWorkflowState.Published);
    }

    [Fact]
    public async Task Resolver_ShouldCarryTheStoredTransitionsOnTheStatusItResolves()
    {
        // Arrange
        // A scoped assignment rather than the organization-wide one, which other classes in this shared
        // database reassign; removed afterwards so nothing else sees it.
        var scopeId = Guid.CreateVersion7();
        StatusWorkflow workflow;
        WorkflowAssignment assignment;
        await using (var setup = _fixture.CreateContext())
        {
            workflow = await SeedRestrictedWorkflow(setup);
            workflow.Publish(EventActor.System, Now).IsSuccess.Should().BeTrue();

            assignment = WorkflowAssignment.Create(ProductWorkflowOwners.Product.Key, scopeId, workflow, EventActor.System, Now).Value;
            setup.WorkflowAssignments.Add(assignment);
            await setup.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        try
        {
            await using var context = _fixture.CreateContext();
            var sut = new StatusResolver(context);

            // Act
            var result = await sut.ForAlias(
                ProductWorkflowOwners.Product.Key, scopeId, (int)ProductStatusAlias.Retired, TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
            result.Value.StatusId.Should().Be(StatusId(workflow, "Retired"));
            result.Value.AllowedFrom.Should().BeEquivalentTo([StatusId(workflow, "Sunset")]);
        }
        finally
        {
            await using var cleanup = _fixture.CreateContext();
            await cleanup.WorkflowAssignments
                .Where(a => a.Id == assignment.Id)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        }
    }
}
