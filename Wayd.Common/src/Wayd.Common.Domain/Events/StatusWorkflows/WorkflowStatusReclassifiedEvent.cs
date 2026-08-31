using NodaTime;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.Events.StatusWorkflows;

/// <summary>
/// A status moved to a different category.
/// </summary>
/// <remarks>
/// Looks like configuration and is not. Every record holding this status rolls up under the new
/// category from now on, so a count of what is Done or Removed changes without any record having
/// moved — the same reason <c>EnvironmentReclassified</c> is its own event rather than part of a
/// generic update.
/// <para>
/// Records carry a denormalized category, so they are stale until remapped; a consumer reading those
/// counts needs to know this happened.
/// </para>
/// <para>
/// Raised by <c>StatusWorkflow.ReclassifyStatus</c>, which is Draft-only for exactly the reason above:
/// on a published workflow the records carrying a denormalized category would all be left disagreeing
/// with the status they point at, and moving those is a remap rather than an edit.
/// </para>
/// </remarks>
public sealed record WorkflowStatusReclassifiedEvent : DomainEvent
{
    public WorkflowStatusReclassifiedEvent(
        Guid workflowId,
        Guid statusId,
        string statusName,
        string ownerType,
        StatusCategory fromCategory,
        StatusCategory toCategory,
        EventActor actor,
        Instant timestamp)
        : base(actor)
    {
        WorkflowId = workflowId;
        StatusId = statusId;
        StatusName = statusName;
        OwnerType = ownerType;
        FromCategory = fromCategory;
        ToCategory = toCategory;

        Timestamp = timestamp;
    }

    public Guid WorkflowId { get; }
    public Guid StatusId { get; }
    public string StatusName { get; }
    public string OwnerType { get; }
    public StatusCategory FromCategory { get; }
    public StatusCategory ToCategory { get; }
}
