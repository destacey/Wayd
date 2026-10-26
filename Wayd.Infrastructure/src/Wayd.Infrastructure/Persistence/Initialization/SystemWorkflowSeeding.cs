using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Infrastructure.Persistence.Activities;

namespace Wayd.Infrastructure.Persistence.Initialization;

/// <summary>
/// Seeds a module's default status workflows: each one published, read-only, and assigned at the
/// organization level.
/// </summary>
/// <remarks>
/// Every module adopting the engine seeds its defaults the same way, so the rules live once. A seeded
/// workflow is read-only, so adding or dropping a status means a clone, a publish and a reassignment;
/// seed only what the owner type's aggregates cannot work without, plus the stages nearly everyone uses.
/// <para>
/// Seeded per owner type rather than all-or-nothing, so a workflow an admin deleted is not silently
/// recreated while a newly added owner type still gets its default.
/// </para>
/// <para>
/// Each default is also <strong>assigned</strong> at the organization level. Publishing only makes a
/// workflow available to assign — without the assignment the resolver has nothing to resolve, and every
/// operation on the owner type fails at runtime with "no workflow is assigned".
/// </para>
/// </remarks>
internal sealed class SystemWorkflowSeeding
{
    private readonly List<SeededWorkflow> _created = [];

    private sealed record SeededWorkflow(StatusWorkflow Workflow, Action[] RaiseCreationEvents);

    /// <summary>
    /// Adds the owner type's default workflow and its organization-level assignment, unless it is already
    /// assigned one.
    /// </summary>
    /// <returns>Whether anything was added, and so whether <see cref="Save"/> has work to do.</returns>
    public async Task<bool> SeedIfAbsent<TAlias>(
        WaydDbContext dbContext,
        WorkflowOwnerDescriptor owner,
        string name,
        string description,
        (string Name, string Description, StatusCategory Category, TAlias Alias)[] statuses,
        IDateTimeProvider dateTimeProvider,
        CancellationToken cancellationToken)
        where TAlias : struct, Enum
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
            var timestamp = dateTimeProvider.Now;

            workflow = StatusWorkflow.CreateSystem(name, description, owner.Key, EventActor.System, timestamp).Value;

            foreach (var (statusName, statusDescription, category, alias) in statuses)
            {
                workflow.AddSystemStatus(statusName, statusDescription, category, Convert.ToInt32(alias), EventActor.System, timestamp);
            }

            // A seeded workflow that cannot satisfy its own owner type is a bug in the seeder, not a
            // runtime condition — fail the boot rather than leave an unusable default in the database.
            var publication = workflow.PublishSystem(EventActor.System, timestamp);
            if (publication.IsFailure)
            {
                throw new InvalidOperationException($"The seeded '{name}' is invalid: {publication.Error}");
            }

            // Every creation event waits for the key, so each is a post-persistence action. They are taken
            // off the workflow so SaveChanges cannot publish them, and recorded by RecordCreationActivity.
            _created.Add(new SeededWorkflow(workflow, [.. workflow.PostPersistenceActions]));
            workflow.ClearDomainEvents();
            workflow.ClearPostPersistenceActions();

            dbContext.StatusWorkflows.Add(workflow);
        }

        // Scope null: the organization-level assignment, which is also the fallback for every narrower
        // scope without one of its own, so the resolver always finds a workflow.
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

    /// <summary>
    /// Saves what <see cref="SeedIfAbsent"/> added, with the activity of each workflow it created.
    /// </summary>
    public async Task Save(WaydDbContext dbContext, CancellationToken cancellationToken)
    {
        // One transaction: a workflow committed without its activity would stay that way, because the
        // assignment it is saved with stops every later run from seeding it again.
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var unitOfWork = await dbContext.BeginUnitOfWork(cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            RecordCreationActivity(dbContext);
            await dbContext.SaveChangesAsync(cancellationToken);

            await unitOfWork.CommitAsync(cancellationToken);
        });
    }

    /// <summary>
    /// Writes the events a seeded workflow's creation raised straight into the activity log, as a workflow
    /// created through the API records them.
    /// </summary>
    /// <remarks>
    /// Not left for <c>SaveChanges</c> to drain: draining also publishes, and Wolverine cannot route during
    /// <c>InitializeDatabases</c>, which runs before the host starts. The events wait for the workflow's key,
    /// so they are raised only once it has been saved.
    /// </remarks>
    private void RecordCreationActivity(WaydDbContext dbContext)
    {
        var ordinal = 0;

        foreach (var (workflow, raiseCreationEvents) in _created)
        {
            foreach (var raise in raiseCreationEvents)
            {
                raise();
            }

            foreach (var domainEvent in workflow.DomainEvents)
            {
                dbContext.ActivityLogs.Add(
                    ActivityLogEntryFactory.CreateActivityLogEntry(domainEvent, workflow, ordinal++, correlationId: null));
            }

            workflow.ClearDomainEvents();
        }
    }
}
