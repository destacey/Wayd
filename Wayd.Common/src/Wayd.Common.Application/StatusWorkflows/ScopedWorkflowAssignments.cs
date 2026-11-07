using CSharpFunctionalExtensions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Application.StatusWorkflows.Queries;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.StatusWorkflows;

/// <summary>
/// Which workflow governs one scope, and pointing that scope at another, for a module whose own screens choose
/// a scope's workflow rather than workflow administration in Settings.
/// </summary>
/// <remarks>
/// A module cannot reach the engine's tables — its database view is its own — so it asks through this.
/// Changing the workflow of a scope that holds records moves them too, through the owner type's
/// <see cref="IStatusRecordMigrator"/> and a remap the operator reviews first (<see cref="PreviewChange"/>).
/// <para>
/// For owner types with one level of scope below the organization. Under nested scopes a scope without its own
/// assignment follows the nearest assigned scope above it, not the organization, so <see cref="Current"/> and
/// removing an assignment would disagree with <see cref="IStatusResolver.ForRecordType"/>.
/// </para>
/// </remarks>
public interface IScopedWorkflowAssignments : IScopedService
{
    /// <summary>
    /// The workflow governing the scope, and whether the scope has an assignment of its own rather than
    /// following the organization's.
    /// </summary>
    /// <returns>A failure when nothing is assigned, not even organization-wide.</returns>
    Task<Result<ScopedWorkflow>> Current(string ownerType, Guid scopeId, CancellationToken cancellationToken);

    /// <summary>The published workflows of the owner type, which a scope may be pointed at, by name.</summary>
    Task<IReadOnlyList<WorkflowChoice>> Published(string ownerType, CancellationToken cancellationToken);

    /// <summary>
    /// How moving the scope onto a workflow would land each status of the one it follows now, with the scope's
    /// records on each, and the target's statuses an operator can choose between.
    /// </summary>
    /// <returns>
    /// A failure when the workflow is not one of the owner type's, is not published, or is the one the scope
    /// follows.
    /// </returns>
    Task<Result<ScopedWorkflowChangePreviewDto>> PreviewChange(string ownerType, Guid scopeId, Guid workflowId, CancellationToken cancellationToken);

    /// <summary>
    /// Points a scope at a published workflow of its owner type and moves its records onto it, in one save.
    /// </summary>
    /// <remarks>
    /// The automatic mapping decides what it can and <paramref name="decisions"/> the rest, overriding it where
    /// they say. Only a scope holding records needs them: with none, whatever is left over goes to the target's
    /// initial status. Records move first and the assignment last, so a refusal saves nothing. Choosing the
    /// organization's workflow removes the scope's own assignment, so the scope follows the organization's
    /// again, and a later change to it reaches the scope. A scope already following the workflow keeps its
    /// assignment, and only its records still on another workflow move, so repeating a change picks up a record
    /// created while it ran.
    /// </remarks>
    /// <returns>How many records moved, or <see cref="ConcurrentChangeError"/> when another change to the scope
    /// saved first.</returns>
    Task<Result<int>> Change(
        string ownerType,
        Guid scopeId,
        Guid workflowId,
        IReadOnlyCollection<StatusRemapDecisionDto> decisions,
        EventActor actor,
        Instant timestamp,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes a scope's own assignment, when it has one, because the scope itself is being removed. Does not
    /// save: the caller saves it together with the scope's removal.
    /// </summary>
    Task<Result> RemoveScope(string ownerType, Guid scopeId, EventActor actor, Instant timestamp, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a scope's own assignment, when it has one, from a scope that stays: it follows the organization's
    /// scheme from now on, or none when the organization has no assignment. Does not save.
    /// </summary>
    /// <remarks>
    /// Like <see cref="RemoveScope"/>, it moves no records, so it suits records whose status a source decides
    /// and that land in their new workflow on their own next sync.
    /// </remarks>
    /// <returns>Whether the scope had an assignment to remove.</returns>
    Task<Result<bool>> Unassign(string ownerType, Guid scopeId, EventActor actor, Instant timestamp, CancellationToken cancellationToken);
}

/// <summary>The workflow governing a scope.</summary>
/// <param name="Workflow">The workflow.</param>
/// <param name="IsScopeOwn">Whether the scope has an assignment of its own; false when it follows the organization's.</param>
public sealed record ScopedWorkflow(WorkflowChoice Workflow, bool IsScopeOwn);

/// <summary>A workflow, named enough to choose between.</summary>
public sealed record WorkflowChoice(Guid Id, int Key, string Name, string? Description);

/// <inheritdoc cref="IScopedWorkflowAssignments"/>
public sealed class ScopedWorkflowAssignments(
    IStatusWorkflowDbContext dbContext,
    IEnumerable<IStatusRecordMigrator> migrators,
    IEnumerable<IStatusRecordCounter> counters)
    : IScopedWorkflowAssignments
{
    /// <summary>The refusal for previewing a move onto the workflow the scope already follows.</summary>
    public const string AlreadyFollowsError = "The scope already follows this workflow.";

    /// <summary>The refusal for a workflow that is not published.</summary>
    public const string NotPublishedError = "Only a published workflow can be assigned.";

    /// <summary>The refusal for a change whose save collided with another change to the same scope.</summary>
    public const string ConcurrentChangeError = "Another change to this workflow assignment was saved at the same time.";

    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly IEnumerable<IStatusRecordMigrator> _migrators = migrators;
    private readonly IEnumerable<IStatusRecordCounter> _counters = counters;

    public async Task<Result<ScopedWorkflow>> Current(string ownerType, Guid scopeId, CancellationToken cancellationToken)
    {
        var descriptor = WorkflowOwners.Resolve(ownerType);
        if (descriptor.IsFailure)
            return Result.Failure<ScopedWorkflow>(descriptor.Error);

        var key = descriptor.Value.Key;

        // The scope's own assignment first, then the organization's, as the resolver reads them.
        var assigned = await _dbContext.WorkflowAssignments
            .Where(a => a.OwnerType == key && (a.ScopeId == scopeId || a.ScopeId == null))
            .OrderByDescending(a => a.ScopeId != null)
            .Join(_dbContext.StatusWorkflows, a => a.Scheme!.DefaultWorkflowId, w => w.Id,
                (a, w) => new { IsScopeOwn = a.ScopeId != null, w.Id, w.Key, w.Name, w.Description })
            .FirstOrDefaultAsync(cancellationToken);

        return assigned is null
            ? Result.Failure<ScopedWorkflow>($"No workflow is assigned for {descriptor.Value.DisplayName}. An administrator must assign one.")
            : Result.Success(new ScopedWorkflow(
                new WorkflowChoice(assigned.Id, assigned.Key, assigned.Name, assigned.Description), assigned.IsScopeOwn));
    }

    public async Task<IReadOnlyList<WorkflowChoice>> Published(string ownerType, CancellationToken cancellationToken)
    {
        var descriptor = WorkflowOwners.Resolve(ownerType);
        if (descriptor.IsFailure)
            return [];

        var key = descriptor.Value.Key;

        var workflows = await _dbContext.StatusWorkflows
            .Where(w => w.OwnerType == key && w.State == StatusWorkflowState.Published)
            .Select(w => new { w.Id, w.Key, w.Name, w.Description })
            .ToListAsync(cancellationToken);

        return
        [
            .. workflows
                .OrderBy(w => w.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(w => w.Key)
                .Select(w => new WorkflowChoice(w.Id, w.Key, w.Name, w.Description)),
        ];
    }

    public async Task<Result<ScopedWorkflowChangePreviewDto>> PreviewChange(string ownerType, Guid scopeId, Guid workflowId, CancellationToken cancellationToken)
    {
        var descriptor = WorkflowOwners.Resolve(ownerType);
        if (descriptor.IsFailure)
            return Result.Failure<ScopedWorkflowChangePreviewDto>(descriptor.Error);

        var key = descriptor.Value.Key;

        var target = await _dbContext.StatusWorkflows
            .AsNoTracking()
            .Include(w => w.Statuses)
            .FirstOrDefaultAsync(w => w.Id == workflowId && w.OwnerType == key, cancellationToken);
        if (target is null)
            return Result.Failure<ScopedWorkflowChangePreviewDto>("Status workflow not found.");

        if (target.State != StatusWorkflowState.Published)
            return Result.Failure<ScopedWorkflowChangePreviewDto>(NotPublishedError);

        var current = await Current(key, scopeId, cancellationToken);
        if (current.IsFailure)
            return Result.Failure<ScopedWorkflowChangePreviewDto>(current.Error);
        if (current.Value.Workflow.Id == target.Id)
            return Result.Failure<ScopedWorkflowChangePreviewDto>(AlreadyFollowsError);

        var from = await _dbContext.StatusWorkflows
            .AsNoTracking()
            .Include(w => w.Statuses)
            .FirstAsync(w => w.Id == current.Value.Workflow.Id, cancellationToken);

        var remap = StatusRemap.AutoMap(from, target);
        if (remap.IsFailure)
            return Result.Failure<ScopedWorkflowChangePreviewDto>(remap.Error);

        var counts = await CountRecords(key, from.Id, scopeId, cancellationToken);
        if (counts.IsFailure)
            return Result.Failure<ScopedWorkflowChangePreviewDto>(counts.Error);

        return Result.Success(new ScopedWorkflowChangePreviewDto
        {
            Remap = PreviewStatusRemapQueryHandler.ToPreview(from, target, remap.Value, counts.Value),
            TargetStatuses = PreviewStatusRemapQueryHandler.DescribeTargets(target),
        });
    }

    public async Task<Result<int>> Change(
        string ownerType,
        Guid scopeId,
        Guid workflowId,
        IReadOnlyCollection<StatusRemapDecisionDto> decisions,
        EventActor actor,
        Instant timestamp,
        CancellationToken cancellationToken)
    {
        var descriptor = WorkflowOwners.Resolve(ownerType);
        if (descriptor.IsFailure)
            return Result.Failure<int>(descriptor.Error);

        var key = descriptor.Value.Key;

        var workflow = await _dbContext.StatusWorkflows
            .Include(w => w.Statuses)
            .Include(w => w.Transitions)
                .ThenInclude(t => t.Conditions)
            .AsSplitQuery()
            .FirstOrDefaultAsync(w => w.Id == workflowId && w.OwnerType == key, cancellationToken);
        if (workflow is null)
            return Result.Failure<int>("Status workflow not found.");

        var current = await Current(key, scopeId, cancellationToken);
        if (current.IsSuccess && current.Value.Workflow.Id == workflow.Id)
            return await MoveStragglers(key, scopeId, workflow, decisions, actor, timestamp, cancellationToken);

        // Checked before any record moves, so a refusal never leaves moved records tracked for another save.
        if (workflow.State != StatusWorkflowState.Published)
            return Result.Failure<int>(NotPublishedError);

        var moved = 0;
        StatusRemap? remap = null;
        if (current.IsSuccess)
        {
            var from = await _dbContext.StatusWorkflows
                .Include(w => w.Statuses)
                .FirstAsync(w => w.Id == current.Value.Workflow.Id, cancellationToken);

            var migrated = await MoveRecords(key, scopeId, from, workflow, decisions, actor, timestamp, cancellationToken);
            if (migrated.IsFailure)
                return Result.Failure<int>(migrated.Error);

            (moved, remap) = migrated.Value;
        }

        // Last, so a refusal above leaves the scope on the workflow its records are still on.
        var pointed = await PointScopeAt(key, scopeId, workflow, remap, actor, timestamp, cancellationToken);
        if (pointed.IsFailure)
            return Result.Failure<int>(pointed.Error);

        return await Save(moved, cancellationToken);
    }

    public async Task<Result<bool>> Unassign(string ownerType, Guid scopeId, EventActor actor, Instant timestamp, CancellationToken cancellationToken)
    {
        var descriptor = WorkflowOwners.Resolve(ownerType);
        if (descriptor.IsFailure)
            return Result.Failure<bool>(descriptor.Error);

        var key = descriptor.Value.Key;

        var own = await OwnAssignment(key, scopeId, cancellationToken);
        if (own is null)
            return Result.Success(false);

        var organization = await OrganizationScheme(key, cancellationToken);
        var removed = own.Remove(organization?.SchemeId, organization?.WorkflowId, actor, timestamp);
        if (removed.IsFailure)
            return Result.Failure<bool>(removed.Error);

        _dbContext.WorkflowAssignments.Remove(own);

        return Result.Success(true);
    }

    public async Task<Result> RemoveScope(string ownerType, Guid scopeId, EventActor actor, Instant timestamp, CancellationToken cancellationToken)
    {
        var descriptor = WorkflowOwners.Resolve(ownerType);
        if (descriptor.IsFailure)
            return Result.Failure(descriptor.Error);

        var key = descriptor.Value.Key;

        var own = await OwnAssignment(key, scopeId, cancellationToken);
        if (own is null)
            return Result.Success();

        var removed = own.Remove(organizationSchemeId: null, organizationWorkflowId: null, actor, timestamp);
        if (removed.IsFailure)
            return removed;

        _dbContext.WorkflowAssignments.Remove(own);

        return Result.Success();
    }

    /// <summary>
    /// Moves the scope's records still on another workflow onto the one the scope already follows: records
    /// created while the scope was changing, which landed on the workflow it was leaving.
    /// </summary>
    /// <returns>How many records moved; none when the owner type cannot be asked.</returns>
    private async Task<Result<int>> MoveStragglers(
        string ownerType,
        Guid scopeId,
        StatusWorkflow workflow,
        IReadOnlyCollection<StatusRemapDecisionDto> decisions,
        EventActor actor,
        Instant timestamp,
        CancellationToken cancellationToken)
    {
        var counter = _counters.FirstOrDefault(c => string.Equals(c.OwnerType, ownerType, StringComparison.OrdinalIgnoreCase));
        var migrator = _migrators.FirstOrDefault(m => string.Equals(m.OwnerType, ownerType, StringComparison.OrdinalIgnoreCase));
        if (counter is null || migrator is null)
            return Result.Success(0);

        var others = await counter.WorkflowsBesides(workflow.Id, scopeId, cancellationToken);
        if (others.IsFailure)
            return Result.Failure<int>(others.Error);
        if (others.Value.Count == 0)
            return Result.Success(0);

        var moved = 0;
        foreach (var fromId in others.Value)
        {
            var from = await _dbContext.StatusWorkflows
                .Include(w => w.Statuses)
                .FirstAsync(w => w.Id == fromId, cancellationToken);

            var remap = StatusRemaps.Build(from, workflow, decisions);
            if (remap.IsFailure)
                return Result.Failure<int>(remap.Error);

            var migrated = await migrator.Migrate(remap.Value, scopeId, actor, timestamp, cancellationToken);
            if (migrated.IsFailure)
                return Result.Failure<int>(migrated.Error);

            moved += migrated.Value;
        }

        return await Save(moved, cancellationToken);
    }

    /// <summary>Saves the change, answering a collision with another change to the same scope as a refusal.</summary>
    private async Task<Result<int>> Save(int moved, CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two changes giving the same scope its own assignment meet at the one-assignment-per-scope index.
            return Result.Failure<int>(ConcurrentChangeError);
        }

        return Result.Success(moved);
    }

    /// <summary>
    /// The scope's records on each status of a workflow, or none when the owner type has no counter to ask.
    /// </summary>
    private async Task<Result<IReadOnlyDictionary<Guid, int>>> CountRecords(string ownerType, Guid workflowId, Guid scopeId, CancellationToken cancellationToken)
    {
        var counter = _counters.FirstOrDefault(c => string.Equals(c.OwnerType, ownerType, StringComparison.OrdinalIgnoreCase));

        return counter is null
            ? Result.Success<IReadOnlyDictionary<Guid, int>>(new Dictionary<Guid, int>())
            : await counter.CountByStatus(workflowId, scopeId, cancellationToken);
    }

    /// <summary>
    /// Moves the scope's records from one workflow to the other, leaving them tracked for the caller's save.
    /// </summary>
    /// <returns>How many records moved, and the remap that moved them.</returns>
    private async Task<Result<(int Moved, StatusRemap Remap)>> MoveRecords(
        string ownerType,
        Guid scopeId,
        StatusWorkflow from,
        StatusWorkflow to,
        IReadOnlyCollection<StatusRemapDecisionDto> decisions,
        EventActor actor,
        Instant timestamp,
        CancellationToken cancellationToken)
    {
        var counts = await CountRecords(ownerType, from.Id, scopeId, cancellationToken);
        if (counts.IsFailure)
            return Result.Failure<(int, StatusRemap)>(counts.Error);

        var holdsRecords = counts.Value.Values.Any(c => c > 0);

        var remap = holdsRecords ? StatusRemaps.Build(from, to, decisions) : StatusRemaps.ForNoRecords(from, to);
        if (remap.IsFailure)
            return Result.Failure<(int, StatusRemap)>(remap.Error);

        var migrator = _migrators.FirstOrDefault(m => string.Equals(m.OwnerType, ownerType, StringComparison.OrdinalIgnoreCase));
        if (migrator is null)
        {
            // Refused when records are there: the scope would claim a workflow its records are not on.
            return holdsRecords
                ? Result.Failure<(int, StatusRemap)>("These records cannot be moved to another workflow.")
                : Result.Success((0, remap.Value));
        }

        // Run even when the count found none, so a record that arrived since is moved rather than left on the
        // workflow the scope is leaving.
        var migrated = await migrator.Migrate(remap.Value, scopeId, actor, timestamp, cancellationToken);

        return migrated.IsFailure
            ? Result.Failure<(int, StatusRemap)>(migrated.Error)
            : Result.Success((migrated.Value, remap.Value));
    }

    /// <summary>
    /// Gives the scope an assignment naming the workflow, reassigns the one it has, or removes it when the
    /// workflow is the organization's. Does not save.
    /// </summary>
    /// <param name="remap">The remap that moved the scope's records, or null when nothing governed the scope.</param>
    private async Task<Result> PointScopeAt(
        string ownerType, Guid scopeId, StatusWorkflow workflow, StatusRemap? remap, EventActor actor, Instant timestamp, CancellationToken cancellationToken)
    {
        var own = await OwnAssignment(ownerType, scopeId, cancellationToken);
        var organization = await OrganizationScheme(ownerType, cancellationToken);

        if (own is not null && workflow.Id == organization?.WorkflowId)
        {
            var removed = own.Remove(organization.SchemeId, organization.WorkflowId, actor, timestamp);
            if (removed.IsFailure)
                return removed;

            _dbContext.WorkflowAssignments.Remove(own);
            return Result.Success();
        }

        var scheme = await _dbContext.ImplicitSchemeFor(workflow, actor, timestamp, cancellationToken);
        if (scheme.IsFailure)
            return Result.Failure(scheme.Error);

        if (own is null)
        {
            var created = WorkflowAssignment.Create(ownerType, scopeId, scheme.Value, workflow, actor, timestamp);
            if (created.IsFailure)
            {
                DiscardIfUnsaved(scheme.Value);
                return Result.Failure(created.Error);
            }

            _dbContext.WorkflowAssignments.Add(created.Value);
            return Result.Success();
        }

        var reassigned = own.ReassignTo(scheme.Value, workflow, remap is null ? [] : [remap], actor, timestamp);
        if (reassigned.IsFailure)
        {
            own.ClearDomainEvents();
            DiscardIfUnsaved(scheme.Value);
        }

        return reassigned;
    }

    /// <summary>The scope's own assignment with its scheme, which reassigning and removing it read.</summary>
    private Task<WorkflowAssignment?> OwnAssignment(string ownerType, Guid scopeId, CancellationToken cancellationToken) =>
        _dbContext.WorkflowAssignments
            .Include(a => a.Scheme)
                .ThenInclude(s => s!.Entries)
            .FirstOrDefaultAsync(a => a.OwnerType == ownerType && a.ScopeId == scopeId, cancellationToken);

    /// <summary>The organization-level assignment's scheme and its default workflow, or null when there is none.</summary>
    private Task<OrganizationAssignment?> OrganizationScheme(string ownerType, CancellationToken cancellationToken) =>
        _dbContext.WorkflowAssignments
            .Where(a => a.OwnerType == ownerType && a.ScopeId == null)
            .Select(a => new OrganizationAssignment(a.SchemeId, a.Scheme!.DefaultWorkflowId))
            .FirstOrDefaultAsync(cancellationToken);

    private sealed record OrganizationAssignment(Guid SchemeId, Guid WorkflowId);

    /// <summary>
    /// Takes back an implicit scheme this call added but will not save, so a caller saving other work in the
    /// same context does not persist it.
    /// </summary>
    private void DiscardIfUnsaved(StatusWorkflowScheme scheme)
    {
        if (scheme.Key == 0)
            _dbContext.StatusWorkflowSchemes.Remove(scheme);
    }
}
