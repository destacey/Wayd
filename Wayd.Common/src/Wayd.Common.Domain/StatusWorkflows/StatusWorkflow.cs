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
    public Result Update(string name, string? description)
    {
        if (IsSystem)
        {
            return Result.Failure("System workflows cannot be modified. Clone this workflow to change it.");
        }

        if (State == StatusWorkflowState.Archived)
        {
            return Result.Failure(ArchivedError);
        }

        Name = name;
        Description = description;

        return Result.Success();
    }

    /// <summary>
    /// Adds a status. Draft only, until the remap engine exists.
    /// </summary>
    public Result<WorkflowStatus> AddStatus(string name, string? description, StatusCategory category, int alias = NoAlias)
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

        return Result.Success(status);
    }

    /// <summary>
    /// Removes a status. Draft only: a status held by an existing record needs those records remapped
    /// first.
    /// </summary>
    public Result RemoveStatus(Guid statusId)
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

        return Result.Success();
    }

    /// <summary>
    /// Renames a status. Safe in every state including active, since records hold the id.
    /// </summary>
    public Result RenameStatus(Guid statusId, string name, string? description)
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

        status.Rename(trimmed, description);

        return Result.Success();
    }

    /// <summary>
    /// Reorders the statuses for display. The supplied ids must be exactly the workflow's statuses.
    /// </summary>
    public Result ReorderStatuses(IReadOnlyList<Guid> orderedStatusIds)
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

        for (var i = 0; i < orderedStatusIds.Count; i++)
        {
            _statuses.Single(s => s.Id == orderedStatusIds[i]).Reorder(i + 1);
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

        State = StatusWorkflowState.Published;

        AddDomainEvent(new WorkflowPublishedEvent(Id, Key, Name, OwnerType, _statuses.Count, actor, timestamp));

        return Result.Success();
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

        AddDomainEvent(new WorkflowArchivedEvent(Id, Key, Name, OwnerType, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Copies this workflow into an editable draft — the route by which a seeded default is diverged
    /// from, keeping the originals safe to reseed on upgrade.
    /// </summary>
    public StatusWorkflow Clone(string name, string? description = null)
    {
        var clone = new StatusWorkflow(name, description ?? Description, OwnerType, isSystem: false);

        foreach (var status in _statuses.OrderBy(s => s.Order))
        {
            clone._statuses.Add(new WorkflowStatus(clone.Id, status.Name, status.Description, status.Category, status.Alias, status.Order));
        }

        return clone;
    }

    /// <summary>
    /// Creates an empty draft workflow for an owner type.
    /// </summary>
    public static Result<StatusWorkflow> Create(string name, string? description, string ownerType)
    {
        var descriptor = WorkflowOwners.Resolve(ownerType);

        return descriptor.IsFailure
            ? Result.Failure<StatusWorkflow>(descriptor.Error)
            : Result.Success(new StatusWorkflow(name, description, descriptor.Value.Key, isSystem: false));
    }

    /// <summary>
    /// Creates a platform-seeded workflow. Read-only; published via <see cref="PublishSystem"/>.
    /// </summary>
    public static Result<StatusWorkflow> CreateSystem(string name, string? description, string ownerType)
    {
        var descriptor = WorkflowOwners.Resolve(ownerType);

        return descriptor.IsFailure
            ? Result.Failure<StatusWorkflow>(descriptor.Error)
            : Result.Success(new StatusWorkflow(name, description, descriptor.Value.Key, isSystem: true));
    }

    /// <summary>
    /// Adds a status to a seeded workflow, bypassing the read-only guard. For the seeder that builds
    /// the workflow; the resulting statuses are themselves marked system-owned.
    /// </summary>
    public WorkflowStatus AddSystemStatus(string name, string? description, StatusCategory category, int alias)
    {
        var order = _statuses.Count == 0 ? 1 : _statuses.Max(s => s.Order) + 1;
        var status = new WorkflowStatus(Id, name, description, category, alias, order, isSystem: true);
        _statuses.Add(status);

        return status;
    }

    /// <summary>
    /// Publishes a seeded workflow, bypassing the system read-only guard but not the alias check.
    /// </summary>
    public Result PublishSystem()
    {
        var guard = GuardRequiredAliases();
        if (guard.IsFailure)
        {
            return guard;
        }

        State = StatusWorkflowState.Published;

        return Result.Success();
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
