using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.Planning;

/// <summary>
/// Where a sprint's <see cref="SprintType"/> came from. The type is worked out when read, so this says which
/// rule decided it.
/// </summary>
/// <remarks>
/// Members are listed in precedence order: the first that applies to a sprint decides its type.
/// </remarks>
public enum SprintTypeSource
{
    [Display(Name = "Team", Description = "The team set the sprint's type.", Order = 1)]
    Team = 1,

    [Display(Name = "Planning Interval", Description = "The category of the planning interval iteration the sprint is mapped to.", Order = 2)]
    PlanningInterval = 2,

    [Display(Name = "Default", Description = "The sprint has no type set by the team and no planning interval mapping, so it is standard.", Order = 3)]
    Default = 3,
}
