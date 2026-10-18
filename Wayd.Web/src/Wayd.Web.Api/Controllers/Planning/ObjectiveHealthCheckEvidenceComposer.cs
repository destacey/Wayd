using System.Text.Json;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Extensions;
using Wayd.Planning.Application.PlanningIntervals.Dtos;
using Wayd.Planning.Application.PlanningIntervals.HealthChecks.Queries;
using Wayd.Planning.Application.PlanningIntervals.Queries;
using Wayd.Planning.Application.Risks.Queries;
using Wayd.Web.Api.Dtos.Planning;
using Wayd.Work.Application.WorkItems.Forecasting;
using Wayd.Work.Application.WorkItems.Queries;

namespace Wayd.Web.Api.Controllers.Planning;

/// <summary>
/// Assembles <see cref="ObjectiveHealthCheckEvidenceDto"/> from the queries behind the separate
/// objective endpoints, calling each the way its endpoint does so the evidence matches what those
/// endpoints return.
/// </summary>
public sealed class ObjectiveHealthCheckEvidenceComposer(IDispatcher dispatcher, IFeatureFlags featureFlags, IDateTimeProvider dateTimeProvider)
{
    /// <summary>Activity is read in pages of this size, back to the last check.</summary>
    private const int ActivityPageSize = 50;

    /// <summary>
    /// The most activity pages read. A busy objective's history since its last check is cut there and
    /// flagged, rather than read without bound.
    /// </summary>
    private const int MaxActivityPages = 4;

    private const string ProgressChangedEventType = "PlanningIntervalObjectiveProgressChangedEvent";

    private static readonly JsonSerializerOptions PayloadOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly IFeatureFlags _featureFlags = featureFlags;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    /// <summary>
    /// Gathers the evidence, or returns null when the interval does not exist or the objective is not
    /// in it.
    /// </summary>
    public async Task<ObjectiveHealthCheckEvidenceDto?> Compose(
        string idOrKey,
        string objectiveIdOrKey,
        ForecastOptions forecastOptions,
        bool includeClosedRisks,
        CancellationToken cancellationToken)
    {
        var planningInterval = await _dispatcher.Send(new GetPlanningIntervalQuery(idOrKey), cancellationToken);
        if (planningInterval is null)
            return null;

        var objective = await _dispatcher.Send(new GetPlanningIntervalObjectiveQuery(planningInterval.Id.ToString(), objectiveIdOrKey), cancellationToken);
        if (objective is null)
            return null;

        var schedule = Schedule(planningInterval, objective);

        var workItems = await _dispatcher.Send(new GetExternalObjectWorkItemsQuery(objective.Id), cancellationToken);
        if (workItems is not null && workItems.WorkItems.Count > 0)
            workItems.WorkItems = [.. workItems.WorkItems.OrderBy(w => w.StackRank)];

        var today = _dateTimeProvider.Now.ToDateOnly();
        var piEnd = planningInterval.End.ToDateOnly();
        var metricsEnd = today < piEnd ? today : piEnd;
        var metrics = await _dispatcher.Send(
            new GetExternalObjectWorkItemMetricsQuery(objective.Id, planningInterval.Start.ToDateOnly(), metricsEnd), cancellationToken);

        var forecastEnabled = await _featureFlags.IsEnabled(FeatureFlags.Names.DeliveryForecasting, cancellationToken);
        var forecast = forecastEnabled
            ? await _dispatcher.Send(new GetExternalObjectForecastQuery(objective.Id, schedule.TargetDate, forecastOptions), cancellationToken)
            : null;

        var healthChecks = await _dispatcher.Send(
            new GetPlanningIntervalObjectiveHealthChecksQuery(planningInterval.Id, objective.Id), cancellationToken) ?? [];

        var sinceLastCheck = await ChangesSince(planningInterval.Id, objective, healthChecks.FirstOrDefault()?.ReportedOn, cancellationToken);

        var risks = await _dispatcher.Send(
            new GetRisksByPlanningIntervalQuery(new IdOrKey(planningInterval.Id.ToString()), includeClosedRisks, objective.Team.Id), cancellationToken);

        return new ObjectiveHealthCheckEvidenceDto
        {
            PlanningInterval = planningInterval,
            Objective = objective,
            Schedule = schedule,
            WorkItems = workItems,
            WorkItemMetrics = metrics ?? [],
            Forecast = forecast,
            ForecastUnavailableReason = forecastEnabled ? null : "Delivery forecasting is turned off for this organization.",
            HealthChecks = healthChecks,
            SinceLastCheck = sinceLastCheck,
            TeamRisks = risks,
        };
    }

    private ObjectiveScheduleEvidenceDto Schedule(PlanningIntervalDetailsDto planningInterval, PlanningIntervalObjectiveDetailsDto objective)
    {
        var start = objective.StartDate ?? planningInterval.Start;
        var target = objective.TargetDate ?? planningInterval.End;
        var today = _dateTimeProvider.Today;

        return new ObjectiveScheduleEvidenceDto
        {
            StartDate = start,
            StartDateDefaulted = objective.StartDate is null,
            TargetDate = target,
            TargetDateDefaulted = objective.TargetDate is null,
            Today = today,
            ElapsedShare = ElapsedShare(start, target, today),
        };
    }

    /// <summary>
    /// The share of the window from <paramref name="start"/> to <paramref name="target"/> that has
    /// passed by <paramref name="today"/>, clamped to 0..1. A window of zero length counts as passed
    /// once its day arrives.
    /// </summary>
    public static double ElapsedShare(LocalDate start, LocalDate target, LocalDate today)
    {
        if (today <= start)
            return today < start || target > start ? 0 : 1;
        if (today >= target)
            return 1;

        var total = Period.DaysBetween(start, target);
        return Math.Round((double)Period.DaysBetween(start, today) / total, 4);
    }

    /// <summary>
    /// Reads the objective's activity back to <paramref name="lastCheck"/> (the most recent page when
    /// there is none) and works out the progress recorded at that check from the first progress change
    /// after it: its <c>previousProgress</c> is what the objective stood at when checked.
    /// </summary>
    private async Task<ObjectiveChangesSinceLastCheckDto> ChangesSince(
        Guid planningIntervalId,
        PlanningIntervalObjectiveDetailsDto objective,
        Instant? lastCheck,
        CancellationToken cancellationToken)
    {
        var activity = new List<ActivityLogDto>();
        var reachedLastCheck = false;
        var truncated = false;

        for (var page = 1; ; page++)
        {
            var result = await _dispatcher.Send(
                new GetPlanningIntervalObjectiveActivitiesQuery(new IdOrKey(planningIntervalId.ToString()), new IdOrKey(objective.Id.ToString()), page, ActivityPageSize),
                cancellationToken);
            var entries = result.IsSuccess ? result.Value : null;
            if (entries is null)
                break;

            if (lastCheck is null)
            {
                activity.AddRange(entries.Items);
                truncated = entries.HasNextPage;
                break;
            }

            activity.AddRange(entries.Items.TakeWhile(e => e.Timestamp > lastCheck.Value));
            if (entries.Items.Any(e => e.Timestamp <= lastCheck.Value) || !entries.HasNextPage)
            {
                reachedLastCheck = true;
                break;
            }

            if (page == MaxActivityPages)
            {
                truncated = true;
                break;
            }
        }

        double? progressAtLastCheck = null;
        if (lastCheck is not null && reachedLastCheck)
        {
            var firstProgressChange = activity.LastOrDefault(e => e.EventType == ProgressChangedEventType);
            progressAtLastCheck = firstProgressChange is null
                ? objective.Progress
                : PreviousProgress(firstProgressChange);
        }

        return new ObjectiveChangesSinceLastCheckDto
        {
            LastCheckReportedOn = lastCheck,
            ProgressAtLastCheck = progressAtLastCheck,
            ProgressChange = progressAtLastCheck is null ? null : Math.Round(objective.Progress - progressAtLastCheck.Value, 2),
            Activity = activity,
            ActivityTruncated = truncated,
        };
    }

    private static double? PreviousProgress(ActivityLogDto entry)
    {
        try
        {
            return JsonSerializer.Deserialize<ProgressChangedPayload>(entry.Payload, PayloadOptions)?.PreviousProgress;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record ProgressChangedPayload(double? PreviousProgress);
}
