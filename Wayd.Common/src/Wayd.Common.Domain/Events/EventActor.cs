using System.Text.Json.Serialization;
using Wayd.Common.Domain.Identity;

namespace Wayd.Common.Domain.Events;

/// <summary>
/// How an event came about: the mechanism that performed the change, and the person who originated it.
/// Required on every <see cref="DomainEvent"/>, so an event can never exist without saying who caused it.
/// </summary>
/// <remarks>
/// <para>
/// The two parts answer different questions, and a notification usually needs both. A CSV import run by
/// Alice is <see cref="EventActorKind.Import"/> originated by Alice — telling a watcher "the import Alice
/// started changed this" rather than "Alice changed this" four hundred times. Collapsing them into one
/// field loses that distinction, which is why <see cref="Kind"/> and <see cref="UserId"/> are separate.
/// </para>
/// <para>
/// Constructed in the application layer, which is the only layer that knows the rules — whether this is a
/// user action, an import, a sync or a scheduled job. The domain requires one but never resolves it: it has
/// zero dependencies and cannot read <c>ICurrentUser</c> or any ambient state. Use the named factories
/// rather than the constructor, so the intent is visible at the call site and greppable afterwards.
/// </para>
/// </remarks>
/// <param name="Kind">The mechanism that performed the change.</param>
/// <param name="UserId">
/// The account that originated the change, or <c>null</c> when nothing did — a scheduled job nobody
/// triggered. Present even for non-<see cref="EventActorKind.User"/> kinds whenever a person set the
/// mechanism running: an import carries the account that started it.
/// </param>
public sealed record EventActor(EventActorKind Kind, string? UserId, Guid? EmployeeId = null)
{
    /// <summary>
    /// The platform acting on its own behalf — scheduled jobs, replication, startup work. Attributed to
    /// <see cref="SystemUser.Id"/> and originated by nobody. A distinct construction rather than a default,
    /// for the same reason <c>PpmActor.System</c> is: an accidental default must never look like a real user.
    /// </summary>
    public static EventActor System { get; } = new(EventActorKind.System, SystemUser.Id);

    /// <summary>A signed-in user acting directly.</summary>
    /// <param name="employeeId">
    /// The employee the account is linked to, where there is one. Optional because an account need not
    /// be linked — the caller decides whether that is acceptable, and an actor request that records who
    /// did something marks itself <c>IRequireLinkedEmployee</c> so the link is guaranteed before it runs.
    /// </param>
    public static EventActor User(string userId, Guid? employeeId = null) =>
        new(EventActorKind.User, userId, employeeId);

    /// <summary>
    /// A bulk import, attributed to the account that started it. Distinguishes "the import changed 400
    /// projects" from "this person edited 400 projects one at a time".
    /// </summary>
    /// <param name="employeeId">
    /// The employee the imported row is <em>about</em>, which is not the person who ran the import — an
    /// import carries who did the work in its own data, and that person frequently has no account here at
    /// all. Supplying it is what lets an imported history name the right person rather than the operator.
    /// </param>
    public static EventActor Import(string? originatingUserId, Guid? employeeId = null) =>
        new(EventActorKind.Import, originatingUserId, employeeId);

    /// <summary>
    /// An integration syncing from an external system, optionally attributed to whoever triggered it
    /// (<c>null</c> for a scheduled sync nobody started).
    /// </summary>
    public static EventActor Sync(string? originatingUserId) => new(EventActorKind.Sync, originatingUserId);

    /// <summary>
    /// An anonymous caller — a live request with no authenticated user. Rare, and deliberately explicit so
    /// it reads as a real answer rather than a missing one.
    /// </summary>
    public static EventActor Anonymous { get; } = new(EventActorKind.Anonymous, null);

    /// <summary>
    /// Whether a person can be named as the origin. False for a scheduled job or an anonymous request, so a
    /// notification knows not to say "changed by ...".
    /// </summary>
    [JsonIgnore]
    public bool HasOriginatingUser => !string.IsNullOrEmpty(UserId) && Kind != EventActorKind.System;
}
