using System.ComponentModel.DataAnnotations;

namespace Wayd.Planning.Domain.Enums;

/// <summary>
/// What came of publishing a poker round's consensus to the record it estimates.
/// </summary>
// max length of 32 characters
public enum PokerRoundPublicationOutcome
{
    /// <summary>The record's owner took the consensus.</summary>
    [Display(Name = "Published", Description = "The consensus was written to the record the round estimates.", Order = 1)]
    Published = 1,

    /// <summary>The record's owner refused the consensus, and nothing was written.</summary>
    [Display(Name = "Refused", Description = "The record's area refused the consensus, and nothing was written.", Order = 2)]
    Refused = 2,
}
