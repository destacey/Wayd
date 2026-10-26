using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.ProjectPortfolioManagement.Application.Projects.HealthChecks.Queries;
using Wayd.ProjectPortfolioManagement.Application.Projects.Models;
using Wayd.ProjectPortfolioManagement.Application.Projects.Queries;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Queries;
using Wayd.Web.Api.Dtos.Ppm;
using Wayd.Work.Application.WorkItems.Forecasting;
using Wayd.Work.Application.WorkItems.Queries;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;

namespace Wayd.Web.Api.Controllers.Ppm;

/// <summary>
/// Gathers a project's health-check evidence by dispatching the reads the health-check recipe makes one at
/// a time, so an agent gets them in one call.
/// </summary>
public sealed class ProjectHealthCheckEvidenceComposer(IDispatcher dispatcher, IFeatureFlags featureFlags, IDateTimeProvider dateTimeProvider)
{
    /// <summary>Entries read per activity page while walking the project's history.</summary>
    private const int ActivityPageSize = 100;

    /// <summary>
    /// The most activity pages read back towards the latest check. A project edited this much since its last
    /// check is reported as truncated rather than read without bound.
    /// </summary>
    private const int MaxActivityPages = 5;

    /// <summary>The reason given for the critical-path evidence that is never available.</summary>
    public const string CriticalPathUnavailable =
        "Critical-path analysis is not implemented yet, so there is no critical-path evidence. Say so in the note.";

    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly IFeatureFlags _featureFlags = featureFlags;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    /// <summary>The evidence for the project named by <paramref name="idOrKey"/>, or null when there is none.</summary>
    public async Task<ProjectHealthCheckEvidenceDto?> Compose(string idOrKey, ForecastOptions forecastOptions, CancellationToken cancellationToken)
    {
        var project = await _dispatcher.Send(new GetProjectQuery(idOrKey), cancellationToken);
        if (project is null)
            return null;

        var today = _dateTimeProvider.Today;
        ProjectIdOrKey projectId = project.Id;

        var planSummary = await _dispatcher.Send(new GetProjectPlanSummaryQuery(projectId), cancellationToken);

        // The same rule the plan summary counts by, so the list and the count agree.
        var tasks = await _dispatcher.Send(new GetProjectTasksQuery(projectId), cancellationToken);
        var overdueTasks = tasks
            .Where(t => t.Status.Id is (int)TaskStatus.NotStarted or (int)TaskStatus.InProgress)
            .Where(t => t.PlannedEnd is { } end && end < today)
            .OrderBy(t => t.PlannedEnd)
            .ToList();

        var stages = project.ProjectLifecycle is null
            ? []
            : (await _dispatcher.Send(new GetProjectStagesQuery(project.Id), cancellationToken))
                .Select(s => new ProjectHealthCheckEvidenceStageDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Status = s.Status,
                    Order = s.Order,
                    Start = s.Start,
                    End = s.End,
                    Progress = s.Progress,
                    PastEndNotComplete = s.End is { } end && end < today
                        && s.Status.Id is not ((int)TaskStatus.Completed or (int)TaskStatus.Canceled),
                })
                .ToList();

        var forecastEnabled = await _featureFlags.IsEnabled(FeatureFlags.Names.DeliveryForecasting, cancellationToken);
        var forecast = forecastEnabled
            ? await _dispatcher.Send(new GetProjectForecastQuery(project.Id, project.End, forecastOptions), cancellationToken)
            : null;

        var healthChecks = await _dispatcher.Send(new GetProjectHealthChecksQuery(project.Id), cancellationToken);
        var latestCheck = healthChecks.Count > 0 ? healthChecks[0] : null;

        var (activity, truncated) = await ReadActivitySince(projectId, latestCheck?.ReportedOn, cancellationToken);

        return new ProjectHealthCheckEvidenceDto
        {
            AsOf = today,
            Project = project,
            PlanSummary = planSummary,
            OverdueTasks = overdueTasks,
            Stages = stages,
            Forecast = forecast,
            ForecastUnavailableReason = forecastEnabled
                ? null
                : "Delivery forecasting is turned off (the delivery-forecasting feature flag), so there is no forecast. Leave it out of the classification.",
            CriticalPathUnavailableReason = CriticalPathUnavailable,
            LatestHealthCheck = latestCheck,
            ActivitySinceLatestCheck = activity,
            ActivityTruncated = truncated,
        };
    }

    /// <summary>
    /// Reads activity newest first until it reaches <paramref name="since"/>; the query has no "since"
    /// filter. Without a cutoff it returns the first page.
    /// </summary>
    private async Task<(List<ActivityLogDto> Activity, bool Truncated)> ReadActivitySince(
        ProjectIdOrKey projectId, Instant? since, CancellationToken cancellationToken)
    {
        List<ActivityLogDto> activity = [];
        var maxPages = since is null ? 1 : MaxActivityPages;

        for (var page = 1; page <= maxPages; page++)
        {
            var result = await _dispatcher.Send(new GetProjectActivitiesQuery(projectId, new PageRequest(page, ActivityPageSize)), cancellationToken);
            var items = result.IsSuccess ? result.Value : null;
            if (items is null)
                return (activity, false);

            foreach (var entry in items.Items)
            {
                if (since is not null && entry.Timestamp <= since)
                    return (activity, false);

                activity.Add(entry);
            }

            if (!items.HasNextPage)
                return (activity, false);
        }

        return (activity, true);
    }
}
