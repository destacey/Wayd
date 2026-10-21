using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

// The delivery artifact record, not System.Version.
using Version = Wayd.ProductManagement.Domain.Models.Version;

namespace Wayd.Infrastructure.IntegrationTests.Sut.StatusWorkflows;

/// <summary>
/// Restricted delivery workflows through a real save and the resolver every delivery handler uses, then
/// applied to the aggregates: an optional move the workflow leaves out is refused, and a required one
/// goes through.
/// </summary>
/// <remarks>
/// Product Management resolves the organization-wide assignment, which other classes in this shared
/// database reassign, so each workflow here is resolved through a scoped assignment removed afterwards.
/// The resolver is the same; only the scope differs.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class DeliveryWorkflowTransitionIntegrationTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 5, 4, 10, 0, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    /// <summary>
    /// Saves a published restricted workflow for <paramref name="owner"/> with the seeded statuses,
    /// allowing every declared move except <paramref name="excluded"/>, and assigns it to a new scope.
    /// </summary>
    private async Task<(StatusWorkflow Workflow, WorkflowAssignment Assignment)> SeedAssigned(
        WorkflowOwnerDescriptor owner, params (string From, string To)[] excluded)
    {
        ProductWorkflowOwners.Register();

        await using var setup = _fixture.CreateContext();

        var workflow = StatusWorkflow.Create($"Test 1086 {owner.DisplayName} {Guid.CreateVersion7()}", null, owner.Key, EventActor.System, Now).Value;
        foreach (var (name, category, alias) in SeededStatuses(owner))
        {
            workflow.AddStatus(name, null, category, (int)alias, EventActor.System, Now);
        }

        var moves = workflow.DeclaredMoves()
            .Where(m => !excluded.Contains((Name(workflow, m.FromStatusId), Name(workflow, m.ToStatusId))))
            .Select(m => new WorkflowTransitionDefinition(m.FromStatusId, m.ToStatusId))
            .ToList();
        workflow.SetTransitionMode(TransitionMode.Restricted, EventActor.System, Now);
        workflow.SetTransitions(moves, EventActor.System, Now).IsSuccess.Should().BeTrue();
        workflow.Publish(EventActor.System, Now).IsSuccess.Should().BeTrue();
        setup.StatusWorkflows.Add(workflow);

        var assignment = WorkflowAssignment.Create(owner.Key, Guid.CreateVersion7(), workflow, EventActor.System, Now).Value;
        setup.WorkflowAssignments.Add(assignment);
        await setup.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (workflow, assignment);
    }

    private static (string Name, StatusCategory Category, ProductStatusAlias Alias)[] SeededStatuses(WorkflowOwnerDescriptor owner) =>
        owner == ProductWorkflowOwners.Deployment
            ?
            [
                ("In Progress", StatusCategory.Active, ProductStatusAlias.InProgress),
                ("Succeeded", StatusCategory.Done, ProductStatusAlias.Succeeded),
                ("Failed", StatusCategory.Removed, ProductStatusAlias.Failed),
                ("Rolled Back", StatusCategory.Removed, ProductStatusAlias.RolledBack),
            ]
            :
            [
                ("Planned", StatusCategory.Proposed, ProductStatusAlias.None),
                ("Ready", StatusCategory.Active, ProductStatusAlias.Ready),
                ("Released", StatusCategory.Done, ProductStatusAlias.Released),
                ("Withdrawn", StatusCategory.Removed, ProductStatusAlias.Withdrawn),
            ];

    private static string Name(StatusWorkflow workflow, Guid statusId) => workflow.Statuses.Single(s => s.Id == statusId).Name;

    private static Guid StatusId(StatusWorkflow workflow, string name) => workflow.Statuses.Single(s => s.Name == name).Id;

    private async Task Unassign(WorkflowAssignment assignment)
    {
        await using var cleanup = _fixture.CreateContext();
        await cleanup.WorkflowAssignments
            .Where(a => a.Id == assignment.Id)
            .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<StatusRef> Resolve(WaydDbContext context, WorkflowAssignment assignment, ProductStatusAlias? alias)
    {
        var resolver = new StatusResolver(context);
        var result = alias is { } a
            ? await resolver.ForAlias(assignment.OwnerType, assignment.ScopeId, (int)a, TestContext.Current.CancellationToken)
            : await resolver.Initial(assignment.OwnerType, assignment.ScopeId, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

        return result.Value;
    }

    [Fact]
    public async Task Withdraw_ShouldBeRefused_ForAReleasedVersion_UnderAStoredWorkflowWithoutReleasedToWithdrawn()
    {
        // Arrange
        var (workflow, assignment) = await SeedAssigned(ProductWorkflowOwners.Version, ("Released", "Withdrawn"));

        try
        {
            await using var context = _fixture.CreateContext();
            var version = Version.Create(
                Guid.CreateVersion7(), "4.8.2", null, null, null, true, await Resolve(context, assignment, null),
                "Checkout", EventActor.System, Now).Value;
            version.MarkReleased(Now, await Resolve(context, assignment, ProductStatusAlias.Released), "Checkout", EventActor.System, Now)
                .IsSuccess.Should().BeTrue();

            var withdrawn = await Resolve(context, assignment, ProductStatusAlias.Withdrawn);

            // Act
            var result = version.Withdraw("Pulled.", withdrawn, "Checkout", EventActor.System, Now);

            // Assert
            // Without the transitions loaded the reference would allow any move, and the withdrawal go through.
            withdrawn.AllowedFrom.Should().BeEquivalentTo([StatusId(workflow, "Planned"), StatusId(workflow, "Ready")]);
            result.IsFailure.Should().BeTrue();
            result.Error.Should().Be("The workflow does not allow a move from 'Released' to 'Withdrawn'.");
            version.StatusName.Should().Be("Released");
        }
        finally
        {
            await Unassign(assignment);
        }
    }

    [Fact]
    public async Task Fail_ShouldSucceed_UnderAStoredDeploymentWorkflowWithOnlyTheRequiredMoves()
    {
        // Arrange
        var (workflow, assignment) = await SeedAssigned(ProductWorkflowOwners.Deployment);

        try
        {
            await using var context = _fixture.CreateContext();
            var deployment = Deployment.Create(
                Guid.CreateVersion7(), null, Guid.CreateVersion7(), EnvironmentCategory.Production, null, Now,
                await Resolve(context, assignment, ProductStatusAlias.InProgress), "Production", EventActor.System, Now).Value;

            // Act
            var result = deployment.Fail(Now, null, await Resolve(context, assignment, ProductStatusAlias.Failed), "Production", EventActor.System, Now);

            // Assert
            result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
            deployment.Outcome.Should().Be(ProductStatusAlias.Failed);
            workflow.EnforcesTransitions.Should().BeTrue();
        }
        finally
        {
            await Unassign(assignment);
        }
    }
}
