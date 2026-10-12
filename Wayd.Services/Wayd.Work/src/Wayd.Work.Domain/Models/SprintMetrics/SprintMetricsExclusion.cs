using System.ComponentModel.DataAnnotations;

namespace Wayd.Work.Domain.Models.SprintMetrics;

/// <summary>Why a completed sprint is left out of its team's sprint metrics.</summary>
public enum SprintMetricsExclusion
{
    [Display(Name = "Non-standard", Description = "The sprint is non-standard, so it isn't comparable with the team's other sprints.", Order = 1)]
    NonStandard = 1,

    [Display(Name = "History incomplete", Description = "The history of a workspace holding the sprint's work has not been read in full, so its figures are unreliable.", Order = 2)]
    HistoryIncomplete = 2,

    [Display(Name = "No commitment", Description = "Nothing was in the sprint at its commitment point, so it has no predictability or say/do.", Order = 3)]
    NoCommitment = 3,
}
