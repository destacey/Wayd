using System.Globalization;
using Wayd.Common.Application.Records;
using Wayd.Common.Domain.Authorization;
using Wayd.Planning.Domain;

namespace Wayd.Planning.Application.PlanningIntervals;

/// <summary>
/// Who may read a planning interval objective, by the rule the objective endpoints
/// apply: the View Planning Interval Objectives permission, which admits every objective.
/// </summary>
/// <remarks>
/// An objective is addressed under its planning interval, so its summary carries the interval's key and
/// name as the parent. If objectives ever gain a per-record read rule, it belongs in
/// <see cref="ReadableIds"/> as well as in the objective queries.
/// </remarks>
public sealed class PlanningIntervalObjectiveReadRule(
    IPlanningDbContext planningDbContext,
    ICurrentPrincipal currentPrincipal) : IRecordReadRule
{
    /// <summary>The permission the objective read endpoints require.</summary>
    public static readonly string ViewPermission =
        ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.PlanningIntervalObjectives);

    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    /// <inheritdoc/>
    public string OwnerType => PlanningFollowOwners.PlanningIntervalObjective.Key;

    /// <inheritdoc/>
    public async Task<IQueryable<Guid>?> ReadableIds(CancellationToken cancellationToken) =>
        await _currentPrincipal.HasPermission(ViewPermission, cancellationToken)
            ? _planningDbContext.PlanningIntervalObjectives.Select(o => o.Id)
            : null;

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<Guid, RecordSummary>> Describe(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        var objectives = await _planningDbContext.PlanningIntervalObjectives
            .Where(o => ids.Contains(o.Id))
            .Join(
                _planningDbContext.PlanningIntervals,
                o => o.PlanningIntervalId,
                pi => pi.Id,
                (o, pi) => new { o.Id, o.Key, o.Name, PlanningIntervalKey = pi.Key, PlanningIntervalName = pi.Name })
            .ToListAsync(cancellationToken);

        return objectives.ToDictionary(
            o => o.Id,
            o => new RecordSummary(
                o.Key.ToString(CultureInfo.InvariantCulture),
                o.Name,
                o.PlanningIntervalKey.ToString(CultureInfo.InvariantCulture),
                o.PlanningIntervalName));
    }
}
