using NodaTime;

namespace Wayd.DemandManagement.Domain.Models;

/// <summary>
/// An employee given access to a single intake request beyond its requester and submitter.
/// </summary>
/// <remarks>
/// Access and the right to join the conversation, not a notification list. A participant reads the summary
/// and substance and may reply, but never reads internal notes and never changes the record. The requester
/// and submitter are implicit participants and are never rows here.
/// </remarks>
public sealed class IntakeRequestParticipant
{
    private IntakeRequestParticipant() { }

    internal IntakeRequestParticipant(Guid requestId, Guid employeeId, Guid? addedById, Instant addedOn)
    {
        RequestId = requestId;
        EmployeeId = employeeId;
        AddedById = addedById;
        AddedOn = addedOn;
    }

    /// <summary>The request the employee has access to.</summary>
    public Guid RequestId { get; private init; }

    /// <summary>The employee given access.</summary>
    public Guid EmployeeId { get; private init; }

    /// <summary>The employee who added them, or null when the platform did.</summary>
    public Guid? AddedById { get; private init; }

    /// <summary>When they were added.</summary>
    public Instant AddedOn { get; private init; }
}
