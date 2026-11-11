using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.Work;

/// <summary>
/// How much a team's delivery today calls for someone's attention, from best to worst, so the worst of several
/// is the greatest value.
/// </summary>
// max length of 32 characters
public enum AttentionStatus
{
    [Display(Name = "On Track", Description = "Nothing calls for attention.", Order = 1)]
    OnTrack = 1,

    [Display(Name = "Watch", Description = "Falling behind, but not yet far enough to act on.", Order = 2)]
    Watch = 2,

    [Display(Name = "Needs Attention", Description = "Far enough behind, or stuck, that someone should act.", Order = 3)]
    NeedsAttention = 3,
}
