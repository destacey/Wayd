namespace Wayd.Common.Domain.Events;

/// <summary>
/// The mechanism that caused a domain event. Paired with the originating account on
/// <see cref="EventActor"/>; see that type for why the two are kept separate.
/// </summary>
public enum EventActorKind
{
    /// <summary>A signed-in user acting directly.</summary>
    User = 0,

    /// <summary>The platform acting on its own behalf — scheduled jobs, replication, startup work.</summary>
    System = 1,

    /// <summary>A bulk import, usually attributed to the account that started it.</summary>
    Import = 2,

    /// <summary>An integration syncing from an external system.</summary>
    Sync = 3,

    /// <summary>A live request with no authenticated user.</summary>
    Anonymous = 4,
}
