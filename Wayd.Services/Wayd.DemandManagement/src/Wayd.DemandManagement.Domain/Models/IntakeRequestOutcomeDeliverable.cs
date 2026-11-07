namespace Wayd.DemandManagement.Domain.Models;

/// <summary>
/// One project deliverable an existing project scope outcome is mapped to: part of the project that meets the
/// request. Part of the <see cref="IntakeRequest"/> aggregate, through its outcome.
/// </summary>
/// <remarks>
/// Points at the deliverable by id with no foreign key to Demand Management's copy of it, as the outcome points
/// at its project: the mapping is the record of what was decided, and outlives a deliverable deleted from the
/// project.
/// </remarks>
public sealed class IntakeRequestOutcomeDeliverable
{
    private IntakeRequestOutcomeDeliverable() { }

    internal IntakeRequestOutcomeDeliverable(Guid requestId, Guid targetId, Guid deliverableId)
    {
        RequestId = requestId;
        TargetId = targetId;
        DeliverableId = deliverableId;
    }

    /// <summary>The request the outcome belongs to.</summary>
    public Guid RequestId { get; private init; }

    /// <summary>The project the outcome points at, which the deliverable belongs to.</summary>
    public Guid TargetId { get; private init; }

    /// <summary>The deliverable, by its id in PPM.</summary>
    public Guid DeliverableId { get; private init; }
}
