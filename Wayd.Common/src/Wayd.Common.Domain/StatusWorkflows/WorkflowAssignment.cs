using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Data;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.StatusWorkflows;

namespace Wayd.Common.Domain.StatusWorkflows;

/// <summary>
/// Which workflow governs one kind of record within one scope — a portfolio's projects, an
/// organization's releases.
/// </summary>
/// <remarks>
/// The container decides and every record under it follows, the way a work item takes its process from
/// its project. No child ever diverges from its container, which is why a record stores its status but
/// never its workflow.
/// <para>
/// A <c>null</c> <see cref="ScopeId"/> is the organization-level assignment, and is the mandatory
/// fallback for records whose owner type has no narrower scope. Product Management uses only that
/// today; Project Portfolio Management will scope by portfolio.
/// </para>
/// <para>
/// Several workflows for one owner type are published at once by design — each scope picks its own —
/// so publishing a workflow says only that it is available to assign.
/// </para>
/// </remarks>
public sealed class WorkflowAssignment : BaseAuditableEntity
{
    private WorkflowAssignment() { }

    private WorkflowAssignment(string ownerType, Guid? scopeId, Guid workflowId)
    {
        OwnerType = ownerType;
        ScopeId = scopeId;
        WorkflowId = workflowId;
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

    /// <summary>The workflow records in this scope use.</summary>
    public Guid WorkflowId { get; private set; }

    /// <summary>
    /// Points this scope at a different workflow.
    /// </summary>
    /// <param name="remap">
    /// How every status in the current workflow translates to one in <paramref name="workflow"/>.
    /// Required, and required to be complete.
    /// </param>
    /// <remarks>
    /// <strong>Reassignment alone is not a migration.</strong> Records in this scope hold statuses from
    /// the previous workflow, and statuses are never shared between workflows, so without a mapping they
    /// would be left holding a status their workflow does not contain. Demanding the remap here is what
    /// makes the switch validated rather than repaired — the caller still has to apply it to the
    /// records, but cannot flip the assignment without having decided where they go.
    /// </remarks>
    public Result ReassignTo(StatusWorkflow workflow, StatusRemap remap, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(workflow, nameof(workflow));
        Guard.Against.Null(remap, nameof(remap));

        if (!string.Equals(workflow.OwnerType, OwnerType, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(
                $"A {workflow.OwnerType} workflow cannot govern {OwnerType} records.");
        }

        if (workflow.State != Enums.StatusWorkflowState.Published)
        {
            return Result.Failure("Only a published workflow can be assigned.");
        }

        if (workflow.Id == WorkflowId)
        {
            return Result.Success();
        }

        if (remap.FromWorkflowId != WorkflowId || remap.ToWorkflowId != workflow.Id)
        {
            return Result.Failure("The mapping is between different workflows than this reassignment.");
        }

        if (!remap.IsComplete)
        {
            return Result.Failure(
                $"{remap.Unresolved.Count} status(es) have nowhere to go. Map every status before reassigning.");
        }

        var fromWorkflowId = WorkflowId;
        WorkflowId = workflow.Id;

        AddDomainEvent(new WorkflowAssignedEvent(
            OwnerType, ScopeId, fromWorkflowId, workflow.Id, workflow.Name, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Assigns a workflow to a scope.
    /// </summary>
    /// <param name="scopeId">The scope, or <c>null</c> for the organization-level default.</param>
    public static Result<WorkflowAssignment> Create(string ownerType, Guid? scopeId, StatusWorkflow workflow, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(workflow, nameof(workflow));

        var descriptor = WorkflowOwners.Resolve(ownerType);
        if (descriptor.IsFailure)
        {
            return Result.Failure<WorkflowAssignment>(descriptor.Error);
        }

        if (!string.Equals(workflow.OwnerType, descriptor.Value.Key, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<WorkflowAssignment>(
                $"A {workflow.OwnerType} workflow cannot govern {descriptor.Value.Key} records.");
        }

        if (workflow.State != Enums.StatusWorkflowState.Published)
        {
            return Result.Failure<WorkflowAssignment>("Only a published workflow can be assigned.");
        }

        var assignment = new WorkflowAssignment(descriptor.Value.Key, scopeId, workflow.Id);

        assignment.AddDomainEvent(new WorkflowAssignedEvent(
            assignment.OwnerType, scopeId, fromWorkflowId: null, workflow.Id, workflow.Name, actor, timestamp));

        return Result.Success(assignment);
    }
}
