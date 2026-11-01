using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Data;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.StatusWorkflows;

namespace Wayd.Common.Domain.StatusWorkflows;

/// <summary>
/// Which <see cref="StatusWorkflowScheme"/> governs one kind of record within one scope — a portfolio's
/// projects, an organization's releases.
/// </summary>
/// <remarks>
/// The container and the record's type decide, and every record of one type under one container
/// follows the same workflow: the scheme names a workflow per record type, with a default for the rest.
/// No record diverges from its container, which is why a record stores its status but never its
/// workflow. The assignment holds no entries of its own; a scope that needs to differ is pointed at a
/// clone of the scheme.
/// <para>
/// An owner type without record types never sees schemes: its scopes pick a workflow, and the assignment
/// points at that workflow's implicit default-only scheme.
/// </para>
/// <para>
/// A <c>null</c> <see cref="ScopeId"/> is the organization-level assignment, and is the mandatory
/// fallback for records whose owner type has no narrower scope.
/// </para>
/// <para>
/// Several workflows for one owner type are published at once by design — each scope picks its own —
/// so publishing a workflow says only that it is available to assign.
/// </para>
/// </remarks>
public sealed class WorkflowAssignment : BaseAuditableEntity
{
    private WorkflowAssignment() { }

    private WorkflowAssignment(string ownerType, Guid? scopeId, StatusWorkflowScheme scheme)
    {
        OwnerType = ownerType;
        ScopeId = scopeId;
        SchemeId = scheme.Id;
        Scheme = scheme;
    }

    /// <summary>The registered owner type this assignment governs.</summary>
    public string OwnerType { get; private init; } = default!;

    /// <summary>
    /// The scope this applies to, or <c>null</c> for the organization-level default.
    /// </summary>
    /// <remarks>
    /// Deliberately an untyped <see cref="Guid"/> with no foreign key: what counts as a scope differs
    /// per owner type — a portfolio for a project, nothing at all for a release — so no single table
    /// could be referenced. The owning module knows what its scope ids mean.
    /// </remarks>
    public Guid? ScopeId { get; private init; }

    /// <summary>The scheme records in this scope follow.</summary>
    public Guid SchemeId { get; private set; }

    /// <summary>
    /// The scheme itself. Must be loaded to reassign or remove the assignment, which report the workflows
    /// the scope leaves.
    /// </summary>
    public StatusWorkflowScheme? Scheme { get; private set; }

    /// <summary>
    /// Points this scope at a different scheme.
    /// </summary>
    /// <param name="scheme">The scheme the scope follows from now on.</param>
    /// <param name="defaultWorkflow">That scheme's default workflow, named in the event.</param>
    /// <param name="remaps">
    /// For every record type whose workflow differs between the two schemes
    /// (<see cref="StatusWorkflowScheme.WorkflowChanges"/>), how each status of its current workflow
    /// translates into its new one. Each is required, and required to be complete.
    /// </param>
    /// <remarks>
    /// <strong>Reassignment alone is not a migration.</strong> Records in this scope hold statuses from
    /// their current workflows, and statuses are never shared between workflows, so without a mapping they
    /// would be left holding a status their workflow does not contain. Demanding the remaps here is what
    /// makes the switch validated rather than repaired — the caller still has to apply them to the
    /// records, but cannot flip the assignment without having decided where they go.
    /// </remarks>
    public Result ReassignTo(
        StatusWorkflowScheme scheme, StatusWorkflow defaultWorkflow, IReadOnlyCollection<StatusRemap> remaps, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(scheme, nameof(scheme));
        Guard.Against.Null(remaps, nameof(remaps));

        var current = Scheme ?? throw new InvalidOperationException(
            "The assignment's current scheme must be loaded to reassign it.");

        var target = GuardTarget(OwnerType, scheme, defaultWorkflow);
        if (target.IsFailure)
        {
            return target;
        }

        if (scheme.Id == SchemeId)
        {
            return Result.Success();
        }

        foreach (var change in StatusWorkflowScheme.WorkflowChanges(current, scheme))
        {
            var remap = remaps.FirstOrDefault(r =>
                r.FromWorkflowId == change.FromWorkflowId && r.ToWorkflowId == change.ToWorkflowId);

            if (remap is null)
            {
                return Result.Failure("The mapping is between different workflows than this reassignment.");
            }

            if (!remap.IsComplete)
            {
                return Result.Failure(
                    $"{remap.Unresolved.Count} status(es) have nowhere to go. Map every status before reassigning.");
            }
        }

        var (fromSchemeId, fromWorkflowId) = (SchemeId, current.DefaultWorkflowId);
        SchemeId = scheme.Id;
        Scheme = scheme;

        AddDomainEvent(new WorkflowAssignedEvent(
            OwnerType, ScopeId, fromWorkflowId, defaultWorkflow.Id, defaultWorkflow.Name, fromSchemeId, scheme.Id, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Records that a narrower scope's own assignment is being removed, so the scope follows the
    /// organization's scheme again, or because the scope itself is gone. Removing it is the caller's.
    /// </summary>
    /// <remarks>
    /// Like reassignment, removal is not a migration: the caller guarantees the scope holds no records on this
    /// scheme's workflows, since nothing here moves them. The organization-level assignment is never removed —
    /// it is the fallback every scope relies on.
    /// </remarks>
    /// <param name="organizationSchemeId">
    /// The organization's scheme the scope follows from now on, or <c>null</c> when the scope itself is gone.
    /// </param>
    /// <param name="organizationWorkflowId">That scheme's default workflow, or <c>null</c> with it.</param>
    public Result Remove(Guid? organizationSchemeId, Guid? organizationWorkflowId, EventActor actor, Instant timestamp)
    {
        if (ScopeId is not { } scopeId)
        {
            return Result.Failure("The organization's workflow assignment cannot be removed.");
        }

        var current = Scheme ?? throw new InvalidOperationException(
            "The assignment's scheme must be loaded to remove it.");

        AddDomainEvent(new WorkflowAssignmentRemovedEvent(
            OwnerType, scopeId, current.DefaultWorkflowId, organizationWorkflowId, SchemeId, organizationSchemeId, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Assigns a scheme to a scope.
    /// </summary>
    /// <param name="scopeId">The scope, or <c>null</c> for the organization-level default.</param>
    /// <param name="defaultWorkflow">The scheme's default workflow, named in the event.</param>
    public static Result<WorkflowAssignment> Create(
        string ownerType, Guid? scopeId, StatusWorkflowScheme scheme, StatusWorkflow defaultWorkflow, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(scheme, nameof(scheme));

        var descriptor = WorkflowOwners.Resolve(ownerType);
        if (descriptor.IsFailure)
        {
            return Result.Failure<WorkflowAssignment>(descriptor.Error);
        }

        var target = GuardTarget(descriptor.Value.Key, scheme, defaultWorkflow);
        if (target.IsFailure)
        {
            return Result.Failure<WorkflowAssignment>(target.Error);
        }

        var assignment = new WorkflowAssignment(descriptor.Value.Key, scopeId, scheme);

        assignment.AddDomainEvent(new WorkflowAssignedEvent(
            assignment.OwnerType, scopeId, fromWorkflowId: null, defaultWorkflow.Id, defaultWorkflow.Name,
            fromSchemeId: null, scheme.Id, actor, timestamp));

        return Result.Success(assignment);
    }

    private static Result GuardTarget(string ownerType, StatusWorkflowScheme scheme, StatusWorkflow defaultWorkflow)
    {
        Guard.Against.Null(defaultWorkflow, nameof(defaultWorkflow));

        if (defaultWorkflow.Id != scheme.DefaultWorkflowId)
        {
            throw new ArgumentException("The workflow is not the scheme's default.", nameof(defaultWorkflow));
        }

        if (!string.Equals(scheme.OwnerType, ownerType, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure($"A {scheme.OwnerType} workflow cannot govern {ownerType} records.");
        }

        return defaultWorkflow.State == Enums.StatusWorkflowState.Published
            ? Result.Success()
            : Result.Failure("Only a published workflow can be assigned.");
    }
}
