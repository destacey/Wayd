using System.ComponentModel.DataAnnotations;

namespace Wayd.Planning.Domain.Enums;

/// <summary>
/// How a member takes part in a poker session. Anyone can switch either way at any time.
/// </summary>
// max length of 32 characters
public enum PokerSessionMemberMode
{
    [Display(Name = "Participant", Description = "Votes on the session's rounds and counts toward its voted and waiting totals.", Order = 1)]
    Participant = 1,

    [Display(Name = "Observer", Description = "Watches the session without voting, and is left out of its voted and waiting totals.", Order = 2)]
    Observer = 2,
}
