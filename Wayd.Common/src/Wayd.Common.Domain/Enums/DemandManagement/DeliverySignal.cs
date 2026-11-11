using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.DemandManagement;

/// <summary>
/// How far the delivery of an intake request has got: what each of its outcomes reports, and what the request
/// rolls up from them. The one vocabulary a requester, a desk and an upstream system read, whatever the
/// project or work item behind an outcome calls its own status.
/// </summary>
/// <remarks>
/// Derived when read, never stored. <see cref="Blocked"/> comes only from a target that reports being blocked;
/// no project or work item does yet.
/// </remarks>
public enum DeliverySignal
{
    /// <summary>Committed to, with no work begun.</summary>
    [Display(Name = "Not Started", Description = "No work has begun.", Order = 1)]
    NotStarted = 1,

    /// <summary>Being worked on.</summary>
    [Display(Name = "In Progress", Description = "Work is underway.", Order = 2)]
    InProgress = 2,

    /// <summary>Underway but unable to progress, as the target reports it.</summary>
    [Display(Name = "Blocked", Description = "Work is underway but cannot progress.", Order = 3)]
    Blocked = 3,

    /// <summary>Delivered.</summary>
    [Display(Name = "Done", Description = "Delivered.", Order = 4)]
    Done = 4,

    /// <summary>Dropped without being delivered.</summary>
    [Display(Name = "Cancelled", Description = "Dropped without being delivered.", Order = 5)]
    Cancelled = 5,
}
