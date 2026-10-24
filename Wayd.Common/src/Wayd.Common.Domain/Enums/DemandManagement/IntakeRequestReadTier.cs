using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.DemandManagement;

/// <summary>
/// The tiers an intake request is read in, granted separately. Each field and each event belongs to one.
/// </summary>
/// <remarks>
/// The tiers are the shape a request's DTOs and history are built in, not a filter applied to one shape
/// afterwards: a reader is given the tiers they hold, and a tier they do not hold is never projected,
/// returned or shown from the activity log.
/// </remarks>
public enum IntakeRequestReadTier
{
    /// <summary>
    /// Enough to recognise that an ask exists: number, title, desk, status, requester, channel,
    /// assigned reviewer, tags, and the need-by date with its basis and reference.
    /// </summary>
    [Display(Name = "Summary", Order = 1)]
    Summary = 1,

    /// <summary>
    /// The ask itself and its own people: description, expected benefits, submitter, participants, origin,
    /// evidence, replies, score and ratings.
    /// </summary>
    [Display(Name = "Substance", Order = 2)]
    Substance = 2,

    /// <summary>
    /// Internal notes and triage reasoning. Review only, and never returned, evented or logged without it.
    /// </summary>
    [Display(Name = "Internal", Order = 3)]
    Internal = 3,
}
