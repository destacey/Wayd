using Wayd.Common.Domain.Events.Planning.Iterations;

namespace Wayd.Work.Domain.Models;

/// <summary>
/// Actual dates for some of a team's sprints that <see cref="TeamSprintTimeline.ValidateCorrection"/> accepted
/// together. Only the timeline creates one, so a sprint can't take corrected dates that weren't checked against
/// its neighbours.
/// </summary>
public sealed class TeamSprintCorrection
{
    internal TeamSprintCorrection(IReadOnlyDictionary<Iteration, SprintActualDates> sprints)
    {
        Sprints = new Dictionary<Iteration, SprintActualDates>(sprints);
    }

    public IReadOnlyDictionary<Iteration, SprintActualDates> Sprints { get; }
}
