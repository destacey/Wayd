using System.ComponentModel.DataAnnotations;

namespace Wayd.Planning.Domain.Enums;

// max length of 32 characters
public enum PokerSessionStatus
{
    [Display(Name = "Active", Description = "The poker session is active and accepting votes.", Order = 1)]
    Active = 1,

    [Display(Name = "Completed", Description = "The poker session has been completed.", Order = 2)]
    Completed = 2,
}
