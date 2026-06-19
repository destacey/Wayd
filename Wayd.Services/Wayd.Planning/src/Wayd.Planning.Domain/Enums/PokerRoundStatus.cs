using System.ComponentModel.DataAnnotations;

namespace Wayd.Planning.Domain.Enums;

// max length of 32 characters
public enum PokerRoundStatus
{
    [Display(Name = "Voting", Description = "The round is currently accepting votes.", Order = 1)]
    Voting = 1,

    [Display(Name = "Revealed", Description = "The votes have been revealed.", Order = 2)]
    Revealed = 2,

    [Display(Name = "Accepted", Description = "The consensus estimate has been accepted.", Order = 3)]
    Accepted = 3,
}
