using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.Organization;

/// <summary>
/// Defines the methodology a team uses to manage their work.
/// </summary>
public enum Methodology
{
    /// <summary>
    /// Sprint-based methodology with time-boxed iterations and sprint backlogs.
    /// </summary>
    [Display(Name = "Scrum", Description = "Sprint backlog, time-boxed planning", Order = 1)]
    Scrum = 1,

    /// <summary>
    /// Continuous flow methodology where work is pulled from the product backlog.
    /// </summary>
    [Display(Name = "Kanban", Description = "Pull from product backlog, continuous flow", Order = 2)]
    Kanban = 2
}
