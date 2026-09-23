using System.ComponentModel.DataAnnotations;

namespace Wayd.Work.Application.WorkItems.Dtos;

public enum WorkItemForecastOutcome
{
    [Display(Name = "Forecast", Description = "The work item has a completion forecast.", Order = 1)]
    Forecast = 1,

    [Display(Name = "Done", Description = "All of the work is already done.", Order = 2)]
    AlreadyDone = 2,

    [Display(Name = "Not Enough History", Description = "The teams involved have not finished enough work recently to forecast from.", Order = 3)]
    NotEnoughHistory = 3,

    [Display(Name = "Blocked by Dependency", Description = "A predecessor, direct or further up the chain, cannot be forecast.", Order = 4)]
    BlockedByDependency = 4,

    [Display(Name = "Cannot Forecast", Description = "None of the remaining work can be forecast; see the issues.", Order = 5)]
    CannotForecast = 5,

    [Display(Name = "Nothing Remaining", Description = "There are no open backlog work items to forecast, though not everything is done.", Order = 6)]
    NoRemainingWork = 6,
}
