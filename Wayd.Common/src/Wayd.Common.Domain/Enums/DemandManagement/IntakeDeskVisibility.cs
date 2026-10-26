using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.DemandManagement;

/// <summary>
/// How much of a request on a desk reaches people outside it. Every request on the desk inherits it.
/// </summary>
/// <remarks>
/// A request is read in three tiers: its summary, its substance, and the internal notes only the desk's
/// agents see. This decides the first two for everyone who is not a member of the desk or the request's own
/// people (requester, submitter and participants); the internal tier never leaves the desk whatever it says.
/// <para>
/// Set per desk rather than per request, because the failure mode of a per-request setting is someone
/// forgetting it and a sensitive ask sitting unrestricted. A sensitive request is routed to a private desk.
/// </para>
/// </remarks>
public enum IntakeDeskVisibility
{
    [Display(Name = "Open", Description = "Anyone who can view requests reads the whole request.", Order = 1)]
    Open = 1,

    [Display(Name = "Summary Only", Description = "Anyone who can view requests reads the summary; the detail stays with the desk and the request's own people.", Order = 2)]
    SummaryOnly = 2,

    /// <summary>
    /// Only the desk's members and the request's own people know the request exists.
    /// </summary>
    /// <remarks>
    /// Absent rather than redacted: the request appears in no list, search, count or aggregate for anyone
    /// else, because a count of hidden requests is itself the disclosure.
    /// </remarks>
    [Display(Name = "Private", Description = "Only the desk and the request's own people know the request exists.", Order = 3)]
    Private = 3,
}

public static class IntakeDeskVisibilityExtensions
{
    /// <summary>
    /// Whether a request on a desk of this visibility reaches more people than on one of <paramref name="other"/>:
    /// Open reaches more than Summary Only, which reaches more than Private.
    /// </summary>
    public static bool IsLessPrivateThan(this IntakeDeskVisibility visibility, IntakeDeskVisibility other) => visibility switch
    {
        IntakeDeskVisibility.Open => other is IntakeDeskVisibility.SummaryOnly or IntakeDeskVisibility.Private,
        IntakeDeskVisibility.SummaryOnly => other is IntakeDeskVisibility.Private,
        IntakeDeskVisibility.Private => false,
        _ => throw new ArgumentOutOfRangeException(nameof(visibility), visibility, "Unknown desk visibility."),
    };
}
