using System.ComponentModel.DataAnnotations;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.StatusWorkflows.Dtos;

/// <summary>
/// The status a transition moved out of, as it was called at the time.
/// </summary>
/// <remarks>
/// Carries no alias, because the transition does not freeze one for the outgoing side. See
/// <see cref="StatusTransitionDto.FromStatus"/>.
/// </remarks>
public sealed record FrozenStatusDto
{
    [Required]
    public required Guid Id { get; init; }

    [Required]
    public required string Name { get; init; }

    [Required]
    public required StatusCategory Category { get; init; }
}

/// <summary>
/// One status change from a record's history, as a reader needs it.
/// </summary>
/// <remarks>
/// Every name and category here is the one frozen onto the transition when it happened, not the
/// workflow's current shape — a status renamed since is still reported as what it was called at the
/// time. That is the whole point of the stored history, so this DTO never resolves a status to look up
/// a fresher name.
/// </remarks>
public sealed record StatusTransitionDto
{
    [Required]
    public required Guid Id { get; init; }

    /// <summary>
    /// Orders the history as it actually happened.
    /// </summary>
    /// <remarks>
    /// Exposed so a caller can order on it. <see cref="ChangedOn"/> cannot carry that alone: rows
    /// written in one save share an instant, which an import routinely produces.
    /// </remarks>
    [Required]
    public required int Sequence { get; init; }

    /// <summary>
    /// The status moved out of, or <c>null</c> when the record entered its initial status.
    /// </summary>
    /// <remarks>
    /// Not a <see cref="StatusNavigationDto"/>: the transition freezes an alias for the status moved
    /// into but not for the one moved out of, and <c>NoAlias</c> is a real value meaning "carries no
    /// well-known meaning" rather than "unknown". Filling one in here would report a status that had an
    /// alias as one that had none.
    /// </remarks>
    public FrozenStatusDto? FromStatus { get; init; }

    /// <summary>The status moved into.</summary>
    [Required]
    public required StatusNavigationDto ToStatus { get; init; }

    /// <summary>The workflow that governed this change.</summary>
    [Required]
    public required Guid WorkflowId { get; init; }

    /// <summary>The mechanism that made the change — a user, an import, a sync, the platform.</summary>
    [Required]
    public required SimpleNavigationDto ActorKind { get; init; }

    /// <summary>
    /// The person who made the change, or <c>null</c> when none can be named.
    /// </summary>
    /// <remarks>
    /// The employee rather than the account, because the employee is the person: it is what other
    /// records attribute to, and it is the only attribution an import can offer for someone who has no
    /// account here at all.
    /// <para>
    /// Null for the platform acting on its own behalf, a scheduled job nobody triggered, an anonymous
    /// request, or an account never linked to an employee. It does not by itself mean the system acted —
    /// <see cref="ChangedBySystem"/> answers that.
    /// </para>
    /// </remarks>
    public NavigationDto? ChangedBy { get; init; }

    /// <summary>
    /// The account behind the change, where there was one.
    /// </summary>
    /// <remarks>
    /// Kept alongside <see cref="ChangedBy"/> rather than replaced by it: the two can differ — an import
    /// names the person a row is about while carrying the operator who ran it — and an account with no
    /// employee link still needs attributing to something.
    /// </remarks>
    public UserNavigationDto? ChangedByUser { get; init; }

    /// <summary>
    /// Whether the platform made the change on its own behalf.
    /// </summary>
    /// <remarks>
    /// Resolved from the recorded account against the well-known system id, not from the absence of a
    /// name: a signed-in user whose account has since been removed also resolves to no name and must
    /// not be reported as the system.
    /// </remarks>
    [Required]
    public required bool ChangedBySystem { get; init; }

    [Required]
    public required Instant ChangedOn { get; init; }

    /// <summary>
    /// When the record moved out of <see cref="ToStatus"/>, or <c>null</c> while it is still there.
    /// </summary>
    /// <remarks>
    /// The <see cref="ChangedOn"/> of the record's next transition, whatever that was — a move onto
    /// another workflow ends the stay like any other change.
    /// </remarks>
    public Instant? LeftOn { get; init; }

    /// <summary>
    /// The calendar time the record spent in <see cref="ToStatus"/>, in whole seconds.
    /// </summary>
    /// <remarks>
    /// Measured to <see cref="LeftOn"/>, or to the time of the request while the record is still in the
    /// status, so the figure for the current status grows between reads. Never negative.
    /// </remarks>
    [Required]
    public required long DurationSeconds { get; init; }

    /// <summary>
    /// How many separate stays the record has had in <see cref="ToStatus"/>, this one included.
    /// </summary>
    /// <remarks>
    /// Counted by status id, so a stay under a name the status has since dropped still counts. The same
    /// on every entry for that status.
    /// </remarks>
    [Required]
    public required int StatusVisitCount { get; init; }

    /// <summary>
    /// The calendar time the record has spent in <see cref="ToStatus"/> across all its stays, in whole
    /// seconds. Equal to <see cref="DurationSeconds"/> when there has been only one.
    /// </summary>
    [Required]
    public required long StatusTotalSeconds { get; init; }

    /// <summary>Why the change was made, where a reason was recorded.</summary>
    public string? Reason { get; init; }
}
