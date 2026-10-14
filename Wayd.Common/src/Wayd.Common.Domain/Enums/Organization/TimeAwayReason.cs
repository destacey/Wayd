using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.Organization;

/// <summary>
/// Why an employee is away from all their teams for a stretch of days. Shown only to the employee, the people
/// they report up to, and whoever administers time away; everyone else sees that they are away, not why.
/// </summary>
/// <remarks>
/// Stored by name, so a member may be added or reordered but never renamed. Max length of 32 characters.
/// </remarks>
public enum TimeAwayReason
{
    [Display(Name = "Paid time off", Description = "Vacation, sick leave or any other paid leave.", Order = 1)]
    PaidTimeOff = 1,

    [Display(Name = "Business travel", Description = "Travelling for work and not available to the team.", Order = 2)]
    BusinessTravel = 2,

    [Display(Name = "Training", Description = "A course, conference or other training.", Order = 3)]
    Training = 3,

    [Display(Name = "Other", Description = "Away for any other reason.", Order = 4)]
    Other = 4,
}
