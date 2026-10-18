using Wayd.Common.Domain.Enums.Planning;

namespace Wayd.Planning.Application.PlanningIntervals.SprintMapping;

/// <summary>
/// Pairs one team's sprints with the planning interval iterations they clearly belong to, so they can be mapped
/// without anyone choosing.
/// </summary>
/// <remarks>
/// A sprint belongs to the iteration that holds more than half of its days, so it belongs to one iteration at
/// most, and a sprint split evenly between two belongs to neither. When several sprints belong to one iteration,
/// the one with the most days in it wins, then the one that starts first. A sprint without both dates belongs
/// nowhere. Only matches are returned: which iterations and sprints are still free to map is the caller's call.
/// </remarks>
public static class IterationSprintMatcher
{
    /// <summary>The sprint matched to each iteration that has one, keyed by iteration id.</summary>
    /// <param name="iterations">The iterations a sprint may be mapped to.</param>
    /// <param name="sprints">One team's sprints that may be mapped.</param>
    public static Dictionary<Guid, Guid> Match(IEnumerable<PlanningIntervalIteration> iterations, IEnumerable<PlanningSprint> sprints)
    {
        var candidateIterations = iterations.ToList();

        return sprints
            .Where(s => s.Type == IterationType.Sprint && s.DateRange.Start.HasValue && s.DateRange.End.HasValue)
            .Select(s => (Sprint: s, Home: HomeIteration(s, candidateIterations)))
            .Where(c => c.Home.HasValue)
            .GroupBy(c => c.Home!.Value.IterationId)
            .ToDictionary(
                g => g.Key,
                g => g
                    .OrderByDescending(c => c.Home!.Value.Days)
                    .ThenBy(c => c.Sprint.DateRange.Start)
                    .ThenBy(c => c.Sprint.Id)
                    .First().Sprint.Id);
    }

    private static (Guid IterationId, int Days)? HomeIteration(PlanningSprint sprint, List<PlanningIntervalIteration> iterations)
    {
        var start = sprint.DateRange.Start!.Value;
        var end = sprint.DateRange.End!.Value;
        var sprintDays = Period.DaysBetween(start, end) + 1;

        foreach (var iteration in iterations)
        {
            var days = DaysInCommon(start, end, iteration.DateRange);
            if (days * 2 > sprintDays)
                return (iteration.Id, days);
        }

        return null;
    }

    private static int DaysInCommon(LocalDate start, LocalDate end, LocalDateRange range)
    {
        var from = LocalDate.Max(start, range.Start);
        var to = LocalDate.Min(end, range.End);

        return to < from ? 0 : Period.DaysBetween(from, to) + 1;
    }
}
