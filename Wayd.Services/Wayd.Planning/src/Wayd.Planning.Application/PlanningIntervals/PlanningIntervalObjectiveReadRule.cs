using System.Globalization;
using Wayd.Common.Application.Records;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Extensions;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Domain;

namespace Wayd.Planning.Application.PlanningIntervals;

/// <summary>
/// Who may read a planning interval objective, by the rule the objective endpoints apply: the View Planning
/// Interval Objectives permission, which admits every objective.
/// </summary>
/// <remarks>
/// An objective is addressed under its planning interval, so its summary carries the interval's key and
/// name as the parent, and it is owned by its team. If objectives ever gain a per-record read rule, it
/// belongs in <see cref="Readable"/> as well as in the objective queries.
/// </remarks>
public sealed class PlanningIntervalObjectiveReadRule(
    IPlanningDbContext planningDbContext,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider) : IRecordReadRule
{
    /// <summary>The permission the objective read endpoints require.</summary>
    public static readonly string ViewPermission =
        ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.PlanningIntervalObjectives);

    /// <summary>The statuses of an objective that has finished, whatever the outcome. A missed objective has finished too.</summary>
    private static readonly ObjectiveStatus[] ClosedStatuses =
        [ObjectiveStatus.Completed, ObjectiveStatus.Canceled, ObjectiveStatus.Missed];

    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    /// <inheritdoc/>
    public string OwnerType => PlanningFollowOwners.PlanningIntervalObjective.Key;

    /// <summary>Where each objective status sits in its lifecycle. Missed is an outcome of finishing, so Completed.</summary>
    public static LifecycleCategory LifecycleOf(ObjectiveStatus status) => status switch
    {
        ObjectiveStatus.NotStarted => LifecycleCategory.NotStarted,
        ObjectiveStatus.InProgress => LifecycleCategory.Active,
        ObjectiveStatus.Canceled => LifecycleCategory.Canceled,
        _ => LifecycleCategory.Completed,
    };

    /// <inheritdoc/>
    /// <remarks>A search matches the name anywhere, or the key exactly.</remarks>
    public async Task<IQueryable<RecordFacts>?> Readable(string? search, CancellationToken cancellationToken)
    {
        if (!await _currentPrincipal.HasPermission(ViewPermission, cancellationToken))
        {
            return null;
        }

        var now = _dateTimeProvider.Now;
        var objectives = _planningDbContext.PlanningIntervalObjectives.AsQueryable();

        if (search is not null)
        {
            objectives = int.TryParse(search, NumberStyles.None, CultureInfo.InvariantCulture, out var key)
                ? objectives.Where(o => o.Name.Contains(search) || o.Key == key)
                : objectives.Where(o => o.Name.Contains(search));
        }

        return objectives.Select(o => new RecordFacts
        {
            Id = o.Id,
            IsUnhealthy = o.HealthChecks
                .Where(h => h.Expiration > now)
                .OrderByDescending(h => h.ReportedOn)
                .Select(h => h.Status)
                .FirstOrDefault() == HealthStatus.Unhealthy,
            IsClosed = ClosedStatuses.Contains(o.Status),
        });
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<Guid, RecordSummary>> Describe(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.Now;
        var objectives = await _planningDbContext.PlanningIntervalObjectives
            .Where(o => ids.Contains(o.Id))
            .Join(
                _planningDbContext.PlanningIntervals,
                o => o.PlanningIntervalId,
                pi => pi.Id,
                (o, pi) => new
                {
                    o.Id,
                    o.Key,
                    o.Name,
                    o.Status,
                    o.Progress,
                    o.StartDate,
                    o.TargetDate,
                    TeamName = o.Team.Name,
                    Health = o.HealthChecks
                        .Where(h => h.Expiration > now)
                        .OrderByDescending(h => h.ReportedOn)
                        .Select(h => (HealthStatus?)h.Status)
                        .FirstOrDefault(),
                    PlanningIntervalKey = pi.Key,
                    PlanningIntervalName = pi.Name,
                })
            .ToListAsync(cancellationToken);

        return objectives.ToDictionary(
            o => o.Id,
            o => new RecordSummary
            {
                Key = o.Key.ToString(CultureInfo.InvariantCulture),
                Name = o.Name,
                ParentKey = o.PlanningIntervalKey.ToString(CultureInfo.InvariantCulture),
                ParentName = o.PlanningIntervalName,
                StatusName = o.Status.GetDisplayName(),
                Lifecycle = LifecycleOf(o.Status),
                Health = o.Health,
                Progress = o.Progress,
                People = [new RecordPerson("Team", o.TeamName, null)],
                Start = o.StartDate,
                End = o.TargetDate,
            });
    }
}
