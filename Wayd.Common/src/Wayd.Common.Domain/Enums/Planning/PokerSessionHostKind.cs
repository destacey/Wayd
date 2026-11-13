using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.Planning;

/// <summary>
/// The kind of record that hosts a planning poker session: the area of Wayd the session belongs to, which lists
/// it and decides who runs it.
/// </summary>
public enum PokerSessionHostKind
{
    /// <summary>
    /// An intake desk, whose agents estimate its requests.
    /// </summary>
    [Display(Name = "Intake Desk", Description = "A session an intake desk runs to estimate its requests.", Order = 1)]
    IntakeDesk = 1,
}
