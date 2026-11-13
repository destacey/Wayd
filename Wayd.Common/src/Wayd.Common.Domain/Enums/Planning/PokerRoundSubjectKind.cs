using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.Planning;

/// <summary>
/// The kind of record a planning poker round estimates.
/// </summary>
public enum PokerRoundSubjectKind
{
    /// <summary>
    /// An intake request.
    /// </summary>
    [Display(Name = "Intake Request", Description = "The round estimates an intake request.", Order = 1)]
    IntakeRequest = 1,
}
