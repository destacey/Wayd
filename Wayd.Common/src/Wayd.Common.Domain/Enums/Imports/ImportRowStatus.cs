using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.Imports;

// max length of 32 characters
public enum ImportRowStatus
{
    [Display(Name = "Pending", Description = "Not yet attempted.", Order = 1)]
    Pending = 1,

    [Display(Name = "Succeeded", Description = "Applied. The record it created is identified by CreatedEntityId.", Order = 2)]
    Succeeded = 2,

    [Display(Name = "Failed", Description = "Attempted and rejected. Error carries the reason.", Order = 3)]
    Failed = 3,

    [Display(Name = "Cancelled", Description = "Never attempted, because the import stopped first.", Order = 4)]
    Cancelled = 4,
}
