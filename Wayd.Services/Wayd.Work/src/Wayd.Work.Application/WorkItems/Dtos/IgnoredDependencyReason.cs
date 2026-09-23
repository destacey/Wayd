using System.ComponentModel.DataAnnotations;

namespace Wayd.Work.Application.WorkItems.Dtos;

public enum IgnoredDependencyReason
{
    [Display(Name = "Closes a Cycle", Description = "The dependency closes a dependency cycle, so the forecast could not honor it.", Order = 1)]
    ClosesCycle = 1,

    [Display(Name = "Predecessor Removed", Description = "The predecessor was removed without being completed, so nothing waits on it. The dependency likely needs cleaning up.", Order = 2)]
    PredecessorRemoved = 2,
}
