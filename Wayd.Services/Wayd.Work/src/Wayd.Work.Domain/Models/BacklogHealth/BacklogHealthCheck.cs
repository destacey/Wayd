using System.ComponentModel.DataAnnotations;

namespace Wayd.Work.Domain.Models.BacklogHealth;

public enum BacklogHealthCheck
{
    [Display(Name = "Runway", Description = "How many weeks the backlog lasts at the team's recent throughput.", Order = 1)]
    Runway = 1,

    [Display(Name = "Net Flow", Description = "Work items created for every work item completed in the lookback window.", Order = 2)]
    NetFlow = 2,

    [Display(Name = "WIP Load", Description = "Active work items per team member.", Order = 3)]
    WipLoad = 3,

    [Display(Name = "Stale", Description = "Work items not modified within the stale threshold.", Order = 4)]
    Stale = 4,

    [Display(Name = "Old Proposed", Description = "Proposed work items created before the age threshold.", Order = 5)]
    OldProposed = 5,

    [Display(Name = "Aging WIP", Description = "Active work items open longer than the team's cycle time percentile.", Order = 6)]
    AgingWip = 6,

    [Display(Name = "Missing Story Points", Description = "Work items in the readiness window without an estimate.", Order = 7)]
    MissingStoryPoints = 7,

    [Display(Name = "Oversized", Description = "Work items in the readiness window estimated above the team's story point percentile.", Order = 8)]
    Oversized = 8,

    [Display(Name = "No Parent", Description = "Work items in the readiness window without a parent.", Order = 9)]
    NoParent = 9,

    [Display(Name = "No Project", Description = "Work items in the readiness window not linked to a project.", Order = 10)]
    NoProject = 10,

    [Display(Name = "Unassigned Active", Description = "Active work items with no one assigned.", Order = 11)]
    UnassignedActive = 11,

    [Display(Name = "Carry-over", Description = "Open work items still in a completed sprint.", Order = 12)]
    CarryOver = 12,

    [Display(Name = "Closed Parent", Description = "Open work items whose parent is done or removed.", Order = 13)]
    ClosedParent = 13,

    [Display(Name = "Rank Inversion", Description = "Work items ranked above a predecessor in the team's backlog.", Order = 14)]
    RankInversion = 14,
}
