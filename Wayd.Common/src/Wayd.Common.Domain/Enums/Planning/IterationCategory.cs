using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.Planning;

// max length of 32 characters
public enum IterationCategory
{
    [Display(Name = "Development", Description = "Development iteration/sprint.", Order = 1)]
    Development = 1,

    [Display(Name = "IP", Description = "Innovation and/or planning iteration/sprint.", Order = 2)]
    InnovationAndPlanning = 2,
}