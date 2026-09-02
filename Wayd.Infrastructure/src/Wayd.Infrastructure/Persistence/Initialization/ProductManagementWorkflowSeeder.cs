using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Domain;

namespace Wayd.Infrastructure.Persistence.Initialization;

/// <summary>
/// Seeds the default status workflows for Product Management's four owner types.
/// </summary>
/// <remarks>
/// Deliberately minimal: each workflow carries the aliases its aggregates cannot work without, plus a
/// starting status, and nothing else. An unused seeded status is not free to remove — deleting an
/// occupied one needs the remap engine, which does not exist yet — so under-seeding is recoverable
/// where over-seeding is not.
/// <para>
/// Seeded per owner type rather than all-or-nothing, so a workflow an admin deleted is not silently
/// recreated while a newly added owner type still gets its default.
/// </para>
/// <para>
/// Each default is also <strong>assigned</strong> at the organization level. Publishing only makes a
/// workflow available to assign — without the assignment the resolver has nothing to resolve, and every
/// product operation fails at runtime with "no workflow is assigned".
/// </para>
/// </remarks>
public class ProductManagementWorkflowSeeder : ICustomSeeder
{
    public async Task Initialize(WaydDbContext dbContext, IDateTimeProvider dateTimeProvider, CancellationToken cancellationToken)
    {
        ProductWorkflowOwners.Register();

        var seeded = false;

        seeded |= await SeedIfAbsent(dbContext, ProductWorkflowOwners.Product, "Default Product Workflow",
            "The lifecycle of a product node.",
            [
                ("Concept", "Proposed but not yet in use.", StatusCategory.Proposed, ProductStatusAlias.None),
                ("Active", "Live and in use.", StatusCategory.Active, ProductStatusAlias.Active),
                ("Sunset", "No longer offered, still supported.", StatusCategory.Active, ProductStatusAlias.Sunset),
                ("Retired", "Withdrawn from service.", StatusCategory.Done, ProductStatusAlias.Retired),
            ], dateTimeProvider, cancellationToken);

        seeded |= await SeedIfAbsent(dbContext, ProductWorkflowOwners.Version, "Default Version Workflow",
            "The lifecycle of a versioned cut of one product.",
            [
                ("Planned", "Scheduled but not yet cut.", StatusCategory.Proposed, ProductStatusAlias.None),
                ("Ready", "Cut and ready to ship.", StatusCategory.Active, ProductStatusAlias.Ready),
                ("Released", "Shipped.", StatusCategory.Done, ProductStatusAlias.Released),
                ("Withdrawn", "Pulled after being cut.", StatusCategory.Removed, ProductStatusAlias.Withdrawn),
            ], dateTimeProvider, cancellationToken);

        // Shares the version vocabulary but not its meaning: an announcement is drafted and announced
        // where a version is cut and shipped. Ready is the resting state before the announcement goes
        // out, not evidence that anything was cut.
        seeded |= await SeedIfAbsent(dbContext, ProductWorkflowOwners.Release, "Default Release Workflow",
            "The lifecycle of a release as announced to customers.",
            [
                ("Planned", "Drafted but not yet announced.", StatusCategory.Proposed, ProductStatusAlias.None),
                ("Ready", "Ready to announce.", StatusCategory.Active, ProductStatusAlias.Ready),
                ("Released", "Announced to customers.", StatusCategory.Done, ProductStatusAlias.Released),
                ("Withdrawn", "Retracted after being announced.", StatusCategory.Removed, ProductStatusAlias.Withdrawn),
            ], dateTimeProvider, cancellationToken);

        seeded |= await SeedIfAbsent(dbContext, ProductWorkflowOwners.ReleasePackage, "Default Release Package Workflow",
            "The lifecycle of a coordinated shipment of several component releases.",
            [
                ("Planned", "Assembled but not yet ready.", StatusCategory.Proposed, ProductStatusAlias.None),
                ("Ready", "Ready to ship.", StatusCategory.Active, ProductStatusAlias.Ready),
                ("Released", "Shipped.", StatusCategory.Done, ProductStatusAlias.Released),
                ("Withdrawn", "Pulled after being assembled.", StatusCategory.Removed, ProductStatusAlias.Withdrawn),
            ], dateTimeProvider, cancellationToken);

        seeded |= await SeedIfAbsent(dbContext, ProductWorkflowOwners.Deployment, "Default Deployment Workflow",
            "The outcome of one version or package reaching one environment.",
            [
                ("In Progress", "Under way, with no outcome yet.", StatusCategory.Active, ProductStatusAlias.InProgress),
                ("Succeeded", "Reached its environment.", StatusCategory.Done, ProductStatusAlias.Succeeded),
                ("Failed", "Did not reach its environment.", StatusCategory.Removed, ProductStatusAlias.Failed),
                ("Rolled Back", "Reached its environment and was reverted.", StatusCategory.Removed, ProductStatusAlias.RolledBack),
            ], dateTimeProvider, cancellationToken);

        if (seeded)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task<bool> SeedIfAbsent(
        WaydDbContext dbContext,
        WorkflowOwnerDescriptor owner,
        string name,
        string description,
        (string Name, string Description, StatusCategory Category, ProductStatusAlias Alias)[] statuses,
        IDateTimeProvider dateTimeProvider,
        CancellationToken cancellationToken)
    {
        // Guarded on the assignment rather than the workflow: the assignment is what the resolver
        // needs, so a scope holding a workflow but no assignment fails every operation for this owner
        // type. Guarding on the workflow reads that state as already-seeded and never repairs it.
        if (await dbContext.WorkflowAssignments.AnyAsync(
                a => a.OwnerType == owner.Key && a.ScopeId == null, cancellationToken))
        {
            return false;
        }

        // An existing system workflow is reused rather than duplicated — reachable when a previous run
        // was interrupted between the two inserts, or when the assignment row was removed.
        var workflow = await dbContext.StatusWorkflows
            .FirstOrDefaultAsync(w => w.OwnerType == owner.Key && w.IsSystem, cancellationToken);

        if (workflow is null)
        {
            workflow = StatusWorkflow.CreateSystem(name, description, owner.Key).Value;

            foreach (var (statusName, statusDescription, category, alias) in statuses)
            {
                workflow.AddSystemStatus(statusName, statusDescription, category, (int)alias);
            }

            // A seeded workflow that cannot satisfy its own owner type is a bug in this file, not a
            // runtime condition — fail the boot rather than leave an unusable default in the database.
            var publication = workflow.PublishSystem();
            if (publication.IsFailure)
            {
                throw new InvalidOperationException($"The seeded '{name}' is invalid: {publication.Error}");
            }

            dbContext.StatusWorkflows.Add(workflow);
        }

        // Scope null: Product Management assigns organization-wide. Also the mandatory fallback for any
        // owner type with no narrower scope, so the resolver always finds a workflow.
        var assignment = WorkflowAssignment.Create(
            owner.Key, scopeId: null, workflow, EventActor.System, dateTimeProvider.Now);

        if (assignment.IsFailure)
        {
            throw new InvalidOperationException($"The seeded '{name}' could not be assigned: {assignment.Error}");
        }

        // Seeding a default is not a domain occurrence: nobody moved this scope off one workflow onto
        // another. Leaving the event on a brand-new aggregate also enlists it in the outbox during
        // InitializeDatabases, which runs before the host starts — and Wolverine cannot route until it
        // has.
        assignment.Value.ClearDomainEvents();

        dbContext.WorkflowAssignments.Add(assignment.Value);

        return true;
    }
}
