using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.Planning;

// max length of 32 characters
public enum RiskGrade
{
    [Display(Name = "Low", Order = 1)]
    Low = 1,

    [Display(Name = "Medium", Order = 2)]
    Medium = 2,

    [Display(Name = "High", Order = 3)]
    High = 3,
}
