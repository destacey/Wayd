using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.DemandManagement.Domain.Models;

/// <summary>
/// One thing an accepted intake request became: a project that delivers it.
/// </summary>
/// <remarks>
/// The link lives here and only here: PPM stores nothing about requests, so who may see that a project came
/// from a request is decided by the request's desk, like any other read of it. A request points at a target
/// at most once. The outcome outlives the project: a deleted project's outcome stays as the record of what
/// was decided.
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

    /// <summary>The project the outcome points at, by the id it shares with its source.</summary>
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
