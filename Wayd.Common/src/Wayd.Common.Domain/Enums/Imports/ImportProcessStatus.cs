using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.Imports;

// max length of 32 characters
public enum ImportProcessStatus
{
    [Display(Name = "Queued", Description = "Accepted and waiting for a worker to pick it up.", Order = 1)]
    Queued = 1,

    [Display(Name = "Processing", Description = "A worker is applying the rows.", Order = 2)]
    Processing = 2,

    [Display(Name = "Cancelling", Description = "A cancellation was requested; the worker stops at the next chunk boundary.", Order = 3)]
    Cancelling = 3,

    [Display(Name = "Succeeded", Description = "Every row was applied.", Order = 4)]
    Succeeded = 4,

    [Display(Name = "Partially Succeeded", Description = "Some rows were applied and some failed.", Order = 5)]
    PartiallySucceeded = 5,

    [Display(Name = "Failed", Description = "No rows were applied, or the import could not run to completion.", Order = 6)]
    Failed = 6,

    [Display(Name = "Cancelled", Description = "Stopped on request. Rows applied before the stop remain.", Order = 7)]
    Cancelled = 7,
}
