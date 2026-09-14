using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.Planning;

// max length of 32 characters
public enum RiskCategory
{
    [Display(Name = "Resolved", Description = "The risk is determined to not be a threat at this time. No further action is required.", Order = 1)]
    Resolved = 1,

    [Display(Name = "Owned", Description = "The risk cannot be resolved within the meeting, so a member of the team is selected to ‘own’ the handling of that risk. This person is responsible for making sure that this risk is appropriately managed.", Order = 2)]
    Owned = 2,

    [Display(Name = "Accepted", Description = "The risk cannot be resolved, so it must be accepted as-is and dealt with as necessary.", Order = 3)]
    Accepted = 3,

    [Display(Name = "Mitigated", Description = "Actions were taken to mitigate the risk.", Order = 4)]
    Mitigated = 4,
}
