using Wayd.Common.Domain.Enums.Planning;

namespace Wayd.Web.Api.Models.Work.Sprints;

/// <summary>
/// Sets or clears the type the team set on a sprint.
/// </summary>
public sealed record SetSprintTypeRequest
{
    /// <summary>
    /// The sprint's type, held whatever its planning interval mapping says. Null clears the team's type, so the
    /// sprint follows the category of the planning interval iteration it is mapped to, or is standard when it
    /// is not mapped.
    /// </summary>
    public SprintType? SprintType { get; set; }
}
