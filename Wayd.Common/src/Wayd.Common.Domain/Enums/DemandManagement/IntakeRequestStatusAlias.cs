namespace Wayd.Common.Domain.Enums.DemandManagement;

/// <summary>
/// The well-known meanings an intake request's status can carry, whatever a desk's workflow calls it.
/// </summary>
/// <remarks>
/// Rules bind to these, never to a status name or id, so a desk that renames or adds review stages keeps
/// them: a declined request needs a reason, a duplicate names the request that survives it, and only the
/// requester's withdrawal reaches Withdrawn. Values are persisted on every request and status transition —
/// never renumber one — and <c>0</c> is reserved to match <c>StatusWorkflow.NoAlias</c>.
/// </remarks>
public enum IntakeRequestStatusAlias
{
    /// <summary>No well-known meaning: a review stage a company added, such as In Review.</summary>
    None = 0,

    /// <summary>Received, and waiting for the desk to look at it. Where every request starts.</summary>
    Submitted = 1,

    /// <summary>Waiting for the requester to answer a question from the desk.</summary>
    WaitingOnRequester = 2,

    /// <summary>The desk has decided to deliver it.</summary>
    Accepted = 3,

    /// <summary>Delivered.</summary>
    Fulfilled = 4,

    /// <summary>Worth doing, but not now. Still open, and needs a reason the requester can read.</summary>
    Deferred = 5,

    /// <summary>Not going ahead. Needs a reason the requester can read.</summary>
    Declined = 6,

    /// <summary>Already asked for in another request, which it names.</summary>
    Duplicate = 7,

    /// <summary>Withdrawn by its requester.</summary>
    Withdrawn = 8,
}
