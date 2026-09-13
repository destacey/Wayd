using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Data;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.StatusWorkflows;

/// <summary>
/// A user-configurable set of statuses for one kind of record. Organizations rename, reorder and add
/// statuses; the domain reasons only about each status's <see cref="WorkflowStatus.Category"/> and
/// <see cref="WorkflowStatus.Alias"/>, so configuration cannot break an invariant.
/// </summary>
/// <remarks>
/// Status is data; semantics stay in code. Records store a status id plus a denormalized category, and
/// anything the domain must decide resolves through an alias.
/// <para>
/// Transitions are not modelled: until a <c>WorkflowTransition</c> exists the engine is any-to-any.
/// Aggregates still refuse nonsense on their own terms, which are domain rules rather than workflow
/// configuration.
/// </para>
/// </remarks>
public sealed class StatusWorkflow : BaseAuditableEntity, IHasIdAndKey
{
    /// <summary>
    /// The alias value meaning "no well-known meaning". Every module's alias enum reserves 0 for this,
    /// so the engine can recognise an unaliased status without knowing whose vocabulary is in use.
    /// </summary>
    public const int NoAlias = 0;

    private const string NotDraftError = "Only draft workflows can be restructured.";
    private const string NotArchivableError = "Only published workflows can be archived.";
    private const string AlreadyPublishedError = "The workflow is already published.";
    private const string ArchivedError = "An archived workflow cannot be modified.";

    private readonly List<WorkflowStatus> _statuses = [];

    private StatusWorkflow() { }

    private StatusWorkflow(string name, string? description, string ownerType, bool isSystem)
    {
        Name = name;
        Description = description;
        OwnerType = ownerType;
        IsSystem = isSystem;
        State = StatusWorkflowState.Draft;
    }

    /// <summary>
    /// The unique auto-generated key of the workflow. This is an alternate key to the Id.
    /// </summary>
    public int Key { get; private init; }

    /// <summary>
    /// What the organization calls this workflow (e.g. "Default Release Workflow").
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// Optional explanation shown to administrators choosing between workflows.
    /// </summary>
    public string? Description
    {
        get;
        private set => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// The kind of record this workflow governs, as a registered
    /// <see cref="WorkflowOwnerDescriptor.Key"/>. Fixed at creation: changing it would invalidate every
    /// alias the workflow carries and every record already using it.
    /// </summary>
    /// <remarks>
    /// A string rather than an enum so the engine stays free of any module's record types — see
    /// <see cref="WorkflowOwnerDescriptor"/>. It is persisted, so the module that owns the key must
    /// never change it.
    /// </remarks>
    public string OwnerType { get; private init; } = default!;

    /// <summary>
    /// The workflow's own lifecycle. Only <see cref="StatusWorkflowState.Published"/> workflows are
    /// assignable, and only <see cref="StatusWorkflowState.Draft"/> ones can be restructured.
    /// </summary>
    public StatusWorkflowState State { get; private set; }

    /// <summary>
    /// Whether this is a platform-seeded default. System workflows are read-only; an organization that
    /// wants to diverge clones one rather than editing it, so an upgrade can safely reseed defaults.
    /// </summary>
    public bool IsSystem { get; private init; }

    /// <summary>
    /// The statuses in this workflow, in display order.
    /// </summary>
    public IReadOnlyCollection<WorkflowStatus> Statuses => _statuses.OrderBy(s => s.Order).ToList().AsReadOnly();

    /// <summary>
    /// Resolves the status carrying a well-known meaning, or <c>null</c> when the workflow has none.
    /// The lookup every aggregate uses instead of naming a status.
    /// </summary>
    public WorkflowStatus? StatusFor(int alias) =>
        alias == NoAlias ? null : _statuses.SingleOrDefault(s => s.Alias == alias);

    /// <summary>
    /// The default status for a newly created record: the lowest-ordered
    /// <see cref="StatusCategory.Proposed"/> status, or the lowest-ordered status when none is proposed.
    /// </summary>
    public WorkflowStatus? InitialStatus =>
        _statuses.Where(s => s.Category == StatusCategory.Proposed).OrderBy(s => s.Order).FirstOrDefault()
        ?? _statuses.OrderBy(s => s.Order).FirstOrDefault();

    /// <summary>
    /// The aliases this workflow's owner type cannot function without, from its registered descriptor.
    /// </summary>
    /// <remarks>
    /// Which meanings are mandatory is the module's call, not the engine's.
    /// </remarks>
    public IReadOnlyCollection<int> RequiredAliases =>
        WorkflowOwners.Resolve(OwnerType) is { IsSuccess: true } resolved ? resolved.Value.RequiredAliases : [];

    /// <summary>
    /// Renders an alias for an error message, via the owning module's descriptor.
    /// </summary>
    /// <remarks>
    /// Degrades to the raw number rather than throwing: a bad error message must not mask the error it
    /// describes.
    /// </remarks>
    private string DescribeAlias(int alias)
    {
        var descriptor = WorkflowOwners.Resolve(OwnerType);

        return descriptor.IsSuccess ? descriptor.Value.DescribeAlias(alias) : alias.ToString();
    }

    /// <summary>
    /// Renames the workflow. Safe in any state other than archived.
    /// </summary>
    public Result Update(string name, string? description, EventActor actor, Instant timestamp)
    {
        if (IsSystem)
        {
            return Result.Failure("System workflows cannot be modified. Clone this workflow to change it.");
        }

        if (State == StatusWorkflowState.Archived)
        {
            return Result.Failure(ArchivedError);
        }

        // Compared after assignment, never against the arguments: the setters trim and blank to null.
        var before = new WorkflowDetails(Name, Description);

        Name = name;
        Description = description;

        var after = new WorkflowDetails(Name, Description);
        if (before != after)
        {
            AddKeyedDomainEvent(() => new WorkflowDetailsUpdatedEvent(Id, Key, after.Name, after.Description, before, actor, timestamp));
        }

        return Result.Success();
    }

    /// <summary>
    /// Adds a status. Draft only, until the remap engine exists.
    /// </summary>
    public Result<WorkflowStatus> AddStatus(string name, string? description, StatusCategory category, int alias, EventActor actor, Instant timestamp)
    {
        if (IsSystem)
        {
            return Result.Failure<WorkflowStatus>("System workflows cannot be modified. Clone this workflow to change it.");
        }

        if (State != StatusWorkflowState.Draft)
        {
            return Result.Failure<WorkflowStatus>(NotDraftError);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<WorkflowStatus>("A status must have a name.");
        }

        var trimmed = name.Trim();

        if (_statuses.Any(s => string.Equals(s.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure<WorkflowStatus>($"A status named '{trimmed}' already exists in this workflow.");
        }

        if (alias != NoAlias && _statuses.Any(s => s.Alias == alias))
        {
            return Result.Failure<WorkflowStatus>($"Another status in this workflow is already the '{DescribeAlias(alias)}' status.");
        }

        var order = _statuses.Count == 0 ? 1 : _statuses.Max(s => s.Order) + 1;
        var status = new WorkflowStatus(Id, trimmed, description, category, alias, order);
        _statuses.Add(status);

        RaiseStatusAdded(status, actor, timestamp);

        return Result.Success(status);
    }

    /// <summary>
    /// Removes a status. Draft only: a status held by an existing record needs those records remapped
    /// first.
    /// </summary>
    public Result RemoveStatus(Guid statusId, EventActor actor, Instant timestamp)
    {
        if (IsSystem)
        {
            return Result.Failure("System workflows cannot be modified. Clone this workflow to change it.");
        }

        if (State != StatusWorkflowState.Draft)
        {
            return Result.Failure(NotDraftError);
        }

        var status = _statuses.SingleOrDefault(s => s.Id == statusId);
        if (status is null)
        {
            return Result.Failure("Status not found.");
        }

        _statuses.Remove(status);

        var name = status.Name;
        AddKeyedDomainEvent(() => new WorkflowStatusRemovedEvent(Id, Key, statusId, name, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Renames a status. Safe in every state including active, since records hold the id.
    /// </summary>
    public Result RenameStatus(Guid statusId, string name, string? description, EventActor actor, Instant timestamp)
    {
        if (IsSystem)
        {
            return Result.Failure("System workflows cannot be modified. Clone this workflow to change it.");
        }

        if (State == StatusWorkflowState.Archived)
        {
            return Result.Failure(ArchivedError);
        }

        var status = _statuses.SingleOrDefault(s => s.Id == statusId);
        if (status is null)
        {
            return Result.Failure("Status not found.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure("A status must have a name.");
        }

        var trimmed = name.Trim();

        if (_statuses.Any(s => s.Id != statusId && string.Equals(s.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure($"A status named '{trimmed}' already exists in this workflow.");
        }

        var before = new WorkflowStatusDetails(status.Name, status.Description);

        status.Rename(trimmed, description);

        var after = new WorkflowStatusDetails(status.Name, status.Description);
        if (before != after)
        {
            AddKeyedDomainEvent(() => new WorkflowStatusRenamedEvent(
                Id, Key, statusId, after.Name, after.Description, before, actor, timestamp));
        }

        return Result.Success();
    }

    /// <summary>
    /// Changes a status's category and its well-known meaning.
    /// </summary>
    /// <remarks>
    /// Draft only, unlike <see cref="RenameStatus"/>. A name is cosmetic — records hold the id — but
    /// records also carry a denormalized category and alias, so changing either on a live workflow
    /// would leave every existing record disagreeing with the status it points at. Moving a published
    /// workflow's statuses is a remap, not an edit.
    /// <para>
    /// The alias is included because it is the harder half to recover from: a workflow that cannot
    /// publish for want of a required alias would otherwise need the offending status deleted and
    /// re-added, losing its description and its position.
    /// </para>
    /// </remarks>
    public Result ReclassifyStatus(Guid statusId, StatusCategory category, int alias, EventActor actor, Instant timestamp)
    {
        if (IsSystem)
        {
            return Result.Failure("System workflows cannot be modified. Clone this workflow to change it.");
        }

        if (State != StatusWorkflowState.Draft)
        {
            return Result.Failure(NotDraftError);
        }

        var status = _statuses.SingleOrDefault(s => s.Id == statusId);
        if (status is null)
        {
            return Result.Failure("Status not found.");
        }

        if (alias != NoAlias && _statuses.Any(s => s.Id != statusId && s.Alias == alias))
        {
            return Result.Failure($"Another status in this workflow is already the '{DescribeAlias(alias)}' status.");
        }

        var fromCategory = status.Category;
        var fromAlias = status.Alias;

        status.Reclassify(category);
        status.SetAlias(alias);

        // Two events, not one: the category is what existing records roll up under, and a consumer
        // counting Done or Removed must not be woken by an alias moving between statuses.
        if (fromCategory != category)
        {
            AddDomainEvent(new WorkflowStatusReclassifiedEvent(
                Id, status.Id, status.Name, OwnerType, fromCategory, category, actor, timestamp));
        }

        if (fromAlias != alias)
        {
            AddKeyedDomainEvent(() => new WorkflowStatusAliasChangedEvent(Id, Key, statusId, fromAlias, alias, actor, timestamp));
        }

        return Result.Success();
    }

    /// <summary>
    /// Reorders the statuses for display. The supplied ids must be exactly the workflow's statuses.
    /// </summary>
    public Result ReorderStatuses(IReadOnlyList<Guid> orderedStatusIds, EventActor actor, Instant timestamp)
    {
        if (IsSystem)
        {
            return Result.Failure("System workflows cannot be modified. Clone this workflow to change it.");
        }

        if (State == StatusWorkflowState.Archived)
        {
            return Result.Failure(ArchivedError);
        }

        Guard.Against.Null(orderedStatusIds, nameof(orderedStatusIds));

        if (orderedStatusIds.Count != _statuses.Count || orderedStatusIds.Distinct().Count() != orderedStatusIds.Count
            || orderedStatusIds.Any(id => _statuses.All(s => s.Id != id)))
        {
            return Result.Failure("The supplied statuses must be exactly the statuses in this workflow.");
        }

        var previousOrder = StatusOrder();

        for (var i = 0; i < orderedStatusIds.Count; i++)
        {
            _statuses.Single(s => s.Id == orderedStatusIds[i]).Reorder(i + 1);
        }

        var order = StatusOrder();
        if (!previousOrder.SequenceEqual(order))
        {
            AddKeyedDomainEvent(() => new WorkflowStatusesReorderedEvent(Id, Key, previousOrder, order, actor, timestamp));
        }

        return Result.Success();
    }

    /// <summary>
    /// Publishes the workflow, refusing one that cannot supply its owner type's required
    /// aliases — caught here rather than later, inside an aggregate, on a record already created.
    /// </summary>
    public Result Publish(EventActor actor, Instant timestamp)
    {
        if (IsSystem)
        {
            return Result.Failure("System workflows are published by the seeder that creates them.");
        }

        if (State == StatusWorkflowState.Published)
        {
            return Result.Failure(AlreadyPublishedError);
        }

        if (State == StatusWorkflowState.Archived)
        {
            return Result.Failure(ArchivedError);
        }

        var guard = GuardRequiredAliases();
        if (guard.IsFailure)
        {
            return guard;
        }

        MarkPublished(actor, timestamp);

        return Result.Success();
    }

    private void MarkPublished(EventActor actor, Instant timestamp)
    {
        State = StatusWorkflowState.Published;

        var statusCount = _statuses.Count;
        AddKeyedDomainEvent(() => new WorkflowPublishedEventV2(Id, Key, OwnerType, statusCount, actor, timestamp));
    }

    /// <summary>
    /// Withdraws the workflow from use. Retained, not deleted, so existing records keep resolving.
    /// </summary>
    /// <param name="isAssigned">
    /// Whether any scope currently assigns this workflow. Supplied by the caller, which owns that
    /// query — the aggregate cannot see assignments.
    /// </param>
    /// <remarks>
    /// "In use" means <em>assigned now</em>, not <em>used historically</em>. Records that passed through
    /// this workflow resolve their statuses through it forever, so waiting for those to clear would make
    /// archiving impossible; what must not happen is leaving a scope pointing at a workflow nothing can
    /// be assigned to. Reassign those scopes first, then archive.
    /// </remarks>
    public Result Archive(bool isAssigned, EventActor actor, Instant timestamp)
    {
        if (IsSystem)
        {
            return Result.Failure("System workflows cannot be archived.");
        }

        if (State != StatusWorkflowState.Published)
        {
            return Result.Failure(NotArchivableError);
        }

        if (isAssigned)
        {
            return Result.Failure("This workflow is still assigned. Reassign those scopes to another workflow first.");
        }

        State = StatusWorkflowState.Archived;

        AddKeyedDomainEvent(() => new WorkflowArchivedEventV2(Id, Key, OwnerType, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Copies this workflow into an editable draft — the route by which a seeded default is diverged
    /// from, keeping the originals safe to reseed on upgrade.
    /// </summary>
    public StatusWorkflow Clone(string name, string? description, EventActor actor, Instant timestamp)
    {
        var clone = new StatusWorkflow(name, description ?? Description, OwnerType, isSystem: false);

        foreach (var status in _statuses.OrderBy(s => s.Order))
        {
            clone._statuses.Add(new WorkflowStatus(clone.Id, status.Name, status.Description, status.Category, status.Alias, status.Order));
        }

        clone.RaiseCreated(Id, actor, timestamp);

        return clone;
    }

    /// <summary>
    /// Creates an empty draft workflow for an owner type.
    /// </summary>
    public static Result<StatusWorkflow> Create(string name, string? description, string ownerType, EventActor actor, Instant timestamp) =>
        Create(name, description, ownerType, isSystem: false, actor, timestamp);

    /// <summary>
    /// Creates a platform-seeded workflow. Read-only; published via <see cref="PublishSystem"/>.
    /// </summary>
    public static Result<StatusWorkflow> CreateSystem(string name, string? description, string ownerType, EventActor actor, Instant timestamp) =>
        Create(name, description, ownerType, isSystem: true, actor, timestamp);

    private static Result<StatusWorkflow> Create(string name, string? description, string ownerType, bool isSystem, EventActor actor, Instant timestamp)
    {
        var descriptor = WorkflowOwners.Resolve(ownerType);
        if (descriptor.IsFailure)
        {
            return Result.Failure<StatusWorkflow>(descriptor.Error);
        }

        var workflow = new StatusWorkflow(name, description, descriptor.Value.Key, isSystem);
        workflow.RaiseCreated(sourceWorkflowId: null, actor, timestamp);

        return Result.Success(workflow);
    }

    /// <summary>
    /// Raises the creation event once the first save has assigned <see cref="Key"/>.
    /// </summary>
    /// <remarks>
    /// Everything else is captured now: the seeder adds statuses and publishes before that save, and those
    /// raise their own events.
    /// </remarks>
    private void RaiseCreated(Guid? sourceWorkflowId, EventActor actor, Instant timestamp)
    {
        var (name, description, ownerType, isSystem) = (Name, Description, OwnerType, IsSystem);
        WorkflowStatusValues[] statuses = [.. _statuses
            .OrderBy(s => s.Order)
            .Select(s => new WorkflowStatusValues(s.Id, s.Name, s.Description, s.Category, s.Alias, s.Order))];

        AddPostPersistenceAction(() => AddDomainEvent(new WorkflowCreatedEvent(
            Id, Key, name, description, ownerType, isSystem, sourceWorkflowId, statuses, actor, timestamp)));
    }

    /// <summary>
    /// Adds a status to a seeded workflow, bypassing the read-only guard. For the seeder that builds
    /// the workflow; the resulting statuses are themselves marked system-owned.
    /// </summary>
    public WorkflowStatus AddSystemStatus(string name, string? description, StatusCategory category, int alias, EventActor actor, Instant timestamp)
    {
        var order = _statuses.Count == 0 ? 1 : _statuses.Max(s => s.Order) + 1;
        var status = new WorkflowStatus(Id, name, description, category, alias, order, isSystem: true);
        _statuses.Add(status);

        RaiseStatusAdded(status, actor, timestamp);

        return status;
    }

    private void RaiseStatusAdded(WorkflowStatus status, EventActor actor, Instant timestamp)
    {
        var (statusId, name, description, category, alias, order) =
            (status.Id, status.Name, status.Description, status.Category, status.Alias, status.Order);

        AddKeyedDomainEvent(() => new WorkflowStatusAddedEvent(
            Id, Key, statusId, name, description, category, alias, order, actor, timestamp));
    }

    /// <summary>
    /// Publishes a seeded workflow, bypassing the system read-only guard but not the alias check.
    /// </summary>
    public Result PublishSystem(EventActor actor, Instant timestamp)
    {
        var guard = GuardRequiredAliases();
        if (guard.IsFailure)
        {
            return guard;
        }

        MarkPublished(actor, timestamp);

        return Result.Success();
    }

    private Guid[] StatusOrder() => [.. _statuses.OrderBy(s => s.Order).Select(s => s.Id)];

    /// <summary>
    /// Raises an event that carries <see cref="Key"/>, waiting for the first save to assign it.
    /// </summary>
    /// <remarks>
    /// The factory runs when the event is raised, so everything else it carries must be captured in locals by
    /// the caller — only Key may be read inside it.
    /// </remarks>
    private void AddKeyedDomainEvent(Func<DomainEvent> build)
    {
        if (Key == 0)
            AddPostPersistenceAction(() => AddDomainEvent(build()));
        else
            AddDomainEvent(build());
    }

    /// <summary>
    /// Refuses activation when the workflow cannot answer what its owner type asks of it.
    /// </summary>
    private Result GuardRequiredAliases()
    {
        var descriptor = WorkflowOwners.Resolve(OwnerType);
        if (descriptor.IsFailure)
        {
            return Result.Failure(descriptor.Error);
        }

        var missing = descriptor.Value.RequiredAliases.Where(a => StatusFor(a) is null).ToList();

        return missing.Count == 0
            ? Result.Success()
            : Result.Failure(
                $"A {descriptor.Value.DisplayName} workflow needs a status for each of: {string.Join(", ", missing.Select(descriptor.Value.DescribeAlias))}.");
    }
}
