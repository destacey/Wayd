using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Scoring.Enums;

// max length of 32 characters

public enum ScoringModelState
{
    [Display(Name = "Proposed", Description = "The scoring model is being defined and is not yet available for use.", Order = 1)]
    Proposed = 1,

    [Display(Name = "Active", Description = "The scoring model is approved and available for scoring.", Order = 2)]
    Active = 2,

    [Display(Name = "Archived", Description = "The scoring model is no longer available for new assignments but remains for historical reference.", Order = 3)]
    Archived = 3
}
