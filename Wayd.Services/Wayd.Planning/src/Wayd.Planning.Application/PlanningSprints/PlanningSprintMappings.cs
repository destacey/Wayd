namespace Wayd.Planning.Application.PlanningSprints;

internal static class PlanningSprintMappings
{
    /// <summary>
    /// Unmaps the sprints from every PI that maps them, through the PI so that it records the change. The caller
    /// saves.
    /// </summary>
    /// <remarks>
    /// A soft-deleted PI is included: its mapping rows remain, and their foreign key would refuse to delete the
    /// copy they point at.
    /// </remarks>
    /// <returns>How many PIs changed.</returns>
    public static async Task<int> UnmapFromPlanningIntervals(this IPlanningDbContext planningDbContext,
        IReadOnlyCollection<Guid> sprintIds, EventActor actor, Instant timestamp, CancellationToken cancellationToken)
    {
        if (sprintIds.Count == 0)
            return 0;

        var planningIntervals = await planningDbContext.PlanningIntervals
            .IgnoreQueryFilters()
            .Include(pi => pi.IterationSprints)
            .Where(pi => pi.IterationSprints.Any(s => sprintIds.Contains(s.SprintId)))
            .ToListAsync(cancellationToken);

        foreach (var planningInterval in planningIntervals)
        {
            var mapped = planningInterval.IterationSprints
                .Select(s => s.SprintId)
                .Where(sprintIds.Contains)
                .ToList();
            foreach (var sprintId in mapped)
            {
                planningInterval.UnmapSprint(sprintId, actor, timestamp);
            }
        }

        return planningIntervals.Count;
    }
}
