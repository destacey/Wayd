using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums;

/// <summary>
/// Who writes a record.
/// </summary>
// Max length of 32 characters
public enum Ownership
{
    [Display(Description = "Wayd users create and change the record.")]
    Owned = 0,

    [Display(Description = "A sync from an external system writes the record; users cannot change it.")]
    Managed = 1,

    [Display(Description = "The record is built into Wayd; neither users nor a sync can change it.")]
    System = 2,
}
