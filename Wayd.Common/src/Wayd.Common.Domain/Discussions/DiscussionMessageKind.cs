using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Discussions;

/// <summary>
/// Who a discussion message is for: everyone who can read the conversation, or only its internal audience.
/// </summary>
/// <remarks>
/// Fixed when the message is posted, and a reply in a thread always takes its thread's kind, so a thread is wholly one
/// or the other. Named for its audience rather than its position: a reply in a thread can be either kind. Persisted by
/// name: <strong>never rename a member</strong> once it ships.
/// </remarks>
public enum DiscussionMessageKind
{
    /// <summary>Read by everyone who can read the conversation.</summary>
    [Display(Name = "Public", Description = "Seen by everyone who can read the conversation.", Order = 1)]
    Public = 1,

    /// <summary>Read only by the conversation's internal audience, as its channel type defines it.</summary>
    [Display(Name = "Internal Note", Description = "Seen only by the conversation's internal audience.", Order = 2)]
    InternalNote = 2,
}
