using Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables;

/// <summary>
/// Derives deliverable progress from a project's plan when deliverables are read, through
/// <see cref="DeliverableProgressCalculator"/>.
/// </summary>
public static class ProjectDeliverableProgress
{
    /// <summary>
    /// Returns the deliverables of one project with <see cref="ProjectDeliverableDto.Progress"/> filled in.
    /// </summary>
    /// <remarks>
    /// Reads the project's whole plan, because a linked summary task's progress comes from every leaf beneath it.
    /// The plan is read only when some deliverable has a link.
    /// </remarks>
    public static async Task<List<ProjectDeliverableDto>> WithProgress(
        this IProjectPortfolioManagementDbContext dbContext,
        Guid projectId,
        IReadOnlyCollection<ProjectDeliverableDto> deliverables,
        CancellationToken cancellationToken)
    {
        if (deliverables.All(d => d.LinkedTaskCount == 0))
            return [.. deliverables];

        var deliverableIds = deliverables.Select(d => d.Id).ToList();
        var links = await dbContext.ProjectDeliverableTasks
            .Where(l => deliverableIds.Contains(l.ProjectDeliverableId))
            .Select(l => new { l.ProjectDeliverableId, l.ProjectTaskId })
            .ToListAsync(cancellationToken);

        var plan = await dbContext.ProjectTasks
            .Where(t => t.ProjectId == projectId)
            .Select(t => new
            {
                t.Id,
                t.ParentId,
                t.Type,
                t.Status,
                t.Progress,
                Start = t.PlannedDateRange == null ? (LocalDate?)null : t.PlannedDateRange.Start,
                End = t.PlannedDateRange == null ? null : t.PlannedDateRange.End,
            })
            .ToListAsync(cancellationToken);

        var calculator = new DeliverableProgressCalculator(plan.Select(t => new DeliverableProgressTask(
            t.Id,
            t.ParentId,
            t.Type,
            t.Status,
            t.Progress.Value,
            t.Start is { } start ? new FlexibleDateRange(start, t.End) : null)));
        var linksByDeliverable = links.ToLookup(l => l.ProjectDeliverableId, l => l.ProjectTaskId);

        return [.. deliverables.Select(d => d with { Progress = calculator.Calculate(linksByDeliverable[d.Id]) })];
    }
}
