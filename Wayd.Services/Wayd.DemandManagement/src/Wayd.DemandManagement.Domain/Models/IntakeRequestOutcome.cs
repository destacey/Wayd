using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.DemandManagement.Domain.Models;

/// <summary>
/// One thing an accepted intake request became: a project or work item that delivers it.
/// </summary>
/// <remarks>
/// The link lives here and only here: neither PPM nor Work stores anything about requests, so who may see that
/// a project or work item came from a request is decided by the request's desk, like any other read of it. A
/// request points at a target at most once. The outcome outlives its target: a deleted project's or work
/// item's outcome stays as the record of what was decided.
/// </remarks>
public sealed class IntakeRequestOutcome
{
    private IntakeRequestOutcome() { }

    internal IntakeRequestOutcome(Guid requestId, IntakeRequestOutcomeKind kind, Guid targetId, string? note, Guid addedById, Instant addedOn)
    {
        RequestId = requestId;
        Kind = kind;
        TargetId = targetId;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        AddedById = addedById;
        AddedOn = addedOn;
    }

    /// <summary>The request the outcome belongs to.</summary>
    public Guid RequestId { get; private init; }

    /// <summary>What the request became.</summary>
    public IntakeRequestOutcomeKind Kind { get; private init; }

    /// <summary>
    /// The project or work item the outcome points at, by its id in its own module; <see cref="Kind"/> says
    /// which.
    /// </summary>
    public Guid TargetId { get; private init; }

    /// <summary>
    /// What the outcome covers, in the agent's words. Always present for existing project scope, where it
    /// says what the project absorbed, since the project's own definition is not changed.
    /// </summary>
    public string? Note { get; private init; }

    /// <summary>The employee who recorded the outcome.</summary>
    public Guid AddedById { get; private init; }

    /// <summary>When the outcome was recorded.</summary>
    public Instant AddedOn { get; private init; }
}
