using Microsoft.EntityFrameworkCore;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Infrastructure.StatusWorkflows;
using Wayd.ProductManagement.Domain;

namespace Wayd.Infrastructure.Persistence.Initialization;

/// <summary>
/// Seeds the default status workflows for Product Management's status-tracked owner types.
/// </summary>
/// <remarks>
/// Deliberately minimal: each workflow carries the aliases its aggregates cannot work without, plus a
/// starting status, and nothing else. After adding a status every record still maps on its own, but a
/// dropped status that records hold maps to nothing, and someone has to decide where those records go.
/// Under-seeding is the cheaper mistake. <see cref="SystemWorkflowSeeding"/> holds the rules every module's
/// defaults are seeded by.
/// </remarks>
public class ProductManagementWorkflowSeeder : ICustomSeeder
{
    private const string IncidentLifecycleUpgradeName = "Default Incident Workflow v2";

    public async Task Initialize(WaydDbContext dbContext, IDateTimeProvider dateTimeProvider, CancellationToken cancellationToken)
    {
        ProductWorkflowOwners.Register();

        var seeding = new SystemWorkflowSeeding();
        var seeded = false;

        seeded |= await seeding.SeedIfAbsent(dbContext, ProductWorkflowOwners.Product, "Default Product Workflow",
            "The lifecycle of a product node.",
            [
                ("Concept", "Proposed but not yet in use.", StatusCategory.Proposed, ProductStatusAlias.None),
                ("Active", "Live and in use.", StatusCategory.Active, ProductStatusAlias.Active),
                ("Sunset", "No longer offered, still supported.", StatusCategory.Active, ProductStatusAlias.Sunset),
                ("Retired", "Withdrawn from service.", StatusCategory.Done, ProductStatusAlias.Retired),
            ], dateTimeProvider, cancellationToken);

        seeded |= await seeding.SeedIfAbsent(dbContext, ProductWorkflowOwners.Version, "Default Version Workflow",
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
        seeded |= await seeding.SeedIfAbsent(dbContext, ProductWorkflowOwners.Release, "Default Release Workflow",
            "The lifecycle of a release as announced to customers.",
            [
                ("Planned", "Drafted but not yet announced.", StatusCategory.Proposed, ProductStatusAlias.None),
                ("Ready", "Ready to announce.", StatusCategory.Active, ProductStatusAlias.Ready),
                ("Released", "Announced to customers.", StatusCategory.Done, ProductStatusAlias.Released),
                ("Withdrawn", "Retracted after being announced.", StatusCategory.Removed, ProductStatusAlias.Withdrawn),
            ], dateTimeProvider, cancellationToken);

        seeded |= await seeding.SeedIfAbsent(dbContext, ProductWorkflowOwners.ReleasePackage, "Default Release Package Workflow",
            "The lifecycle of a coordinated shipment of several component releases.",
            [
                ("Planned", "Assembled but not yet ready.", StatusCategory.Proposed, ProductStatusAlias.None),
                ("Ready", "Ready to ship.", StatusCategory.Active, ProductStatusAlias.Ready),
                ("Released", "Shipped.", StatusCategory.Done, ProductStatusAlias.Released),
                ("Withdrawn", "Pulled after being assembled.", StatusCategory.Removed, ProductStatusAlias.Withdrawn),
            ], dateTimeProvider, cancellationToken);

        seeded |= await seeding.SeedIfAbsent(dbContext, ProductWorkflowOwners.Deployment, "Default Deployment Workflow",
            "The outcome of one version or package reaching one environment.",
            [
                ("In Progress", "Under way, with no outcome yet.", StatusCategory.Active, ProductStatusAlias.InProgress),
                ("Succeeded", "Reached its environment.", StatusCategory.Done, ProductStatusAlias.Succeeded),
                ("Failed", "Did not reach its environment.", StatusCategory.Removed, ProductStatusAlias.Failed),
                ("Rolled Back", "Reached its environment and was reverted.", StatusCategory.Removed, ProductStatusAlias.RolledBack),
            ], dateTimeProvider, cancellationToken);

        seeded |= await seeding.SeedIfAbsent(dbContext, ProductWorkflowOwners.Incident, "Default Incident Workflow",
            "The lifecycle of an operational incident.",
            [
                ("Open", "Under investigation.", StatusCategory.Active, ProductStatusAlias.Open),
                ("Responding", "Active response is under way.", StatusCategory.Active, ProductStatusAlias.Responding),
                ("Recovered", "Service restored.", StatusCategory.Done, ProductStatusAlias.Resolved),
                ("Closed", "Closed after recovery.", StatusCategory.Done, ProductStatusAlias.Closed),
                ("Canceled", "Closed without treating as a real incident.", StatusCategory.Removed, ProductStatusAlias.Canceled),
            ], dateTimeProvider, cancellationToken);

        if (seeded)
        {
            await seeding.Save(dbContext, cancellationToken);
        }

        await EnsureIncidentWorkflowLifecycle(dbContext, dateTimeProvider, cancellationToken);
    }

    /// <summary>
    /// Moves the organization assignment onto a published workflow that carries the incident lifecycle
    /// the aggregate expects. System workflows cannot be edited in place — the same route as Settings:
    /// clone, change the draft, publish, reassign, migrate records.
    /// </summary>
    private static async Task EnsureIncidentWorkflowLifecycle(
        WaydDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        CancellationToken cancellationToken)
    {
        var assignment = await dbContext.WorkflowAssignments
            .FirstOrDefaultAsync(
                a => a.OwnerType == ProductWorkflowOwners.Incident.Key && a.ScopeId == null,
                cancellationToken);

        if (assignment is null)
        {
            return;
        }

        var current = await dbContext.StatusWorkflows
            .Include(w => w.Statuses)
            .Include(w => w.Transitions)
            .FirstOrDefaultAsync(w => w.Id == assignment.WorkflowId, cancellationToken);

        if (current is null || IncidentWorkflowHasExpectedLifecycle(current))
        {
            return;
        }

        var timestamp = dateTimeProvider.Now;
        var actor = EventActor.System;

        StatusWorkflow target;

        var existingUpgrade = await dbContext.StatusWorkflows
            .Include(w => w.Statuses)
            .Include(w => w.Transitions)
            .FirstOrDefaultAsync(
                w => w.OwnerType == ProductWorkflowOwners.Incident.Key
                     && w.Name == IncidentLifecycleUpgradeName
                     && w.State == StatusWorkflowState.Published,
                cancellationToken);

        if (existingUpgrade is not null && IncidentWorkflowHasExpectedLifecycle(existingUpgrade))
        {
            target = existingUpgrade;
        }
        else
        {
            var draft = current.Clone(IncidentLifecycleUpgradeName, current.Description, actor, timestamp);
            ApplyIncidentLifecycleStructure(draft, actor, timestamp);

            var publication = draft.Publish(actor, timestamp);
            if (publication.IsFailure)
            {
                throw new InvalidOperationException(
                    $"The incident lifecycle upgrade could not be published: {publication.Error}");
            }

            dbContext.StatusWorkflows.Add(draft);
            target = draft;
        }

        var remap = StatusRemap.AutoMap(current, target);
        if (remap.IsFailure)
        {
            throw new InvalidOperationException(remap.Error);
        }

        if (!remap.Value.IsComplete)
        {
            var unresolved = string.Join(", ", remap.Value.Unresolved.Select(s => s.Name));
            throw new InvalidOperationException(
                $"The incident lifecycle upgrade could not map every status: {unresolved}.");
        }

        var migrator = new IncidentStatusRecordMigrator(dbContext);

        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var unitOfWork = await dbContext.BeginUnitOfWork(cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            var migrated = await migrator.Migrate(remap.Value, scopeId: null, actor, timestamp, cancellationToken);
            if (migrated.IsFailure)
            {
                throw new InvalidOperationException(migrated.Error);
            }

            var reassigned = assignment.ReassignTo(target, remap.Value, actor, timestamp);
            if (reassigned.IsFailure)
            {
                assignment.ClearDomainEvents();
                throw new InvalidOperationException(reassigned.Error);
            }

            assignment.ClearDomainEvents();
            target.ClearDomainEvents();

            await dbContext.SaveChangesAsync(cancellationToken);

            await unitOfWork.CommitAsync(cancellationToken);
        });
    }

    private static bool IncidentWorkflowHasExpectedLifecycle(StatusWorkflow workflow)
    {
        if (workflow.TransitionMode != TransitionMode.Restricted)
        {
            return false;
        }

        var aliases = workflow.Statuses.Select(s => s.Alias).ToHashSet();
        foreach (var required in ProductWorkflowOwners.Incident.RequiredAliases)
        {
            if (!aliases.Contains(required))
            {
                return false;
            }
        }

        WorkflowStatus StatusFor(ProductStatusAlias alias) =>
            workflow.Statuses.Single(s => s.Alias == (int)alias);

        var desired = ProductWorkflowOwners.Incident.Moves
            .Select(m => (From: StatusFor((ProductStatusAlias)m.FromAlias!.Value).Id,
                To: StatusFor((ProductStatusAlias)m.ToAlias!.Value).Id))
            .ToHashSet();

        var current = workflow.Transitions
            .Select(t => (t.FromStatusId, t.ToStatusId))
            .ToHashSet();

        return current.SetEquals(desired);
    }

    private static void ApplyIncidentLifecycleStructure(
        StatusWorkflow workflow,
        EventActor actor,
        Instant timestamp)
    {
        if (workflow.Statuses.All(s => s.Alias != (int)ProductStatusAlias.Responding))
        {
            var added = workflow.AddStatus(
                "Responding",
                "Active response is under way.",
                StatusCategory.Active,
                (int)ProductStatusAlias.Responding,
                actor,
                timestamp);
            if (added.IsFailure)
            {
                throw new InvalidOperationException(added.Error);
            }
        }

        if (workflow.Statuses.All(s => s.Alias != (int)ProductStatusAlias.Closed))
        {
            var added = workflow.AddStatus(
                "Closed",
                "Closed after recovery.",
                StatusCategory.Done,
                (int)ProductStatusAlias.Closed,
                actor,
                timestamp);
            if (added.IsFailure)
            {
                throw new InvalidOperationException(added.Error);
            }
        }

        var resolved = workflow.Statuses.SingleOrDefault(s => s.Alias == (int)ProductStatusAlias.Resolved);
        if (resolved is not null && resolved.Name != "Recovered")
        {
            var renamed = workflow.RenameStatus(
                resolved.Id,
                "Recovered",
                "Service restored.",
                actor,
                timestamp);
            if (renamed.IsFailure)
            {
                throw new InvalidOperationException(renamed.Error);
            }
        }

        if (workflow.TransitionMode != TransitionMode.Restricted)
        {
            var mode = workflow.SetTransitionMode(TransitionMode.Restricted, actor, timestamp);
            if (mode.IsFailure)
            {
                throw new InvalidOperationException(mode.Error);
            }
        }

        WorkflowStatus StatusFor(ProductStatusAlias alias) =>
            workflow.Statuses.Single(s => s.Alias == (int)alias);

        var moves = ProductWorkflowOwners.Incident.Moves
            .Select(m => new WorkflowTransitionDefinition(
                StatusFor((ProductStatusAlias)m.FromAlias!.Value).Id,
                StatusFor((ProductStatusAlias)m.ToAlias!.Value).Id))
            .ToList();

        var current = workflow.Transitions
            .Select(t => (t.FromStatusId, t.ToStatusId))
            .ToHashSet();

        var desired = moves.Select(m => (m.FromStatusId, m.ToStatusId)).ToHashSet();
        if (!current.SetEquals(desired))
        {
            var set = workflow.SetTransitions(moves, actor, timestamp);
            if (set.IsFailure)
            {
                throw new InvalidOperationException(set.Error);
            }
        }
    }
}
