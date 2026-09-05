using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.Imports;

// max length of 32 characters
public enum ImportAtomicity
{
    [Display(Name = "Per Row", Description = "Rows succeed or fail independently; the good ones are kept.", Order = 1)]
    PerRow = 1,

    [Display(Name = "Atomic", Description = "The file applies as one unit; a single bad row keeps all of it out.", Order = 2)]
    Atomic = 2,
}
