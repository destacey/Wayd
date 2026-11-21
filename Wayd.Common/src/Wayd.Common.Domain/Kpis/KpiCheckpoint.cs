using NodaTime;
using Wayd.Common.Domain.Data;
using Wayd.Common.Domain.Enums;

namespace Wayd.Common.Domain.Kpis;

/// <summary>
/// One point of a KPI's checkpoint plan: a day by which the KPI is expected to reach a planned value. Part of the
/// <see cref="Kpi"/> aggregate, changed only through it, and only as a whole plan.
/// </summary>
public sealed class KpiCheckpoint : BaseAuditableEntity
{
    private string _label = default!;

    private KpiCheckpoint() { }

    internal KpiCheckpoint(Guid kpiId, KpiCheckpointValues values)
    {
        KpiId = kpiId;
        Date = values.Date;
        Apply(values);
    }

    /// <summary>The KPI this checkpoint belongs to.</summary>
    public Guid KpiId { get; private init; }

    /// <summary>
    /// The day the planned value is due. Unique within the KPI, and what a plan is matched on when it is replaced, so a
    /// checkpoint moved to another day is removed and added rather than changed.
    /// </summary>
    public LocalDate Date { get; private init; }

    /// <summary>A short name for the checkpoint, such as "Q1" or "Sprint 12".</summary>
    public string Label
    {
        get => _label;
        private set => _label = value.Trim();
    }

    /// <summary>The value the KPI is expected to reach by <see cref="Date"/>.</summary>
    public double PlannedValue { get; private set; }

    /// <summary>
    /// The line short of <see cref="PlannedValue"/> past which a measurement read against this checkpoint is at risk
    /// rather than unhealthy, or null for two-state health up to this checkpoint.
    /// </summary>
    public double? AtRiskValue { get; private set; }

    /// <summary>Takes the plan's values for this checkpoint's day.</summary>
    internal void Apply(KpiCheckpointValues values)
    {
        Label = values.Label;
        PlannedValue = values.PlannedValue;
        AtRiskValue = values.AtRiskValue;
    }

    internal KpiCheckpointValues ToValues() => new(Date, Label, PlannedValue, AtRiskValue);
}

/// <summary>
/// One checkpoint of a plan, as a caller supplies it and as an event and a reading record it.
/// </summary>
/// <param name="Date">The day the planned value is due.</param>
/// <param name="Label">A short name for it, such as "Q1".</param>
/// <param name="PlannedValue">The value the KPI is expected to reach by then.</param>
/// <param name="AtRiskValue">The optional line short of the planned value past which a measurement is at risk.</param>
public sealed record KpiCheckpointValues(LocalDate Date, string Label, double PlannedValue, double? AtRiskValue);

/// <summary>
/// The rules of a KPI's checkpoint plan, and which checkpoint a measurement is read against.
/// </summary>
public static class KpiCheckpointPlan
{
    /// <summary>The longest label a checkpoint holds.</summary>
    public const int LabelMaxLength = 32;

    /// <summary>The most checkpoints a plan holds: two years of weeks, or many years of sprints.</summary>
    public const int MaxCheckpoints = 104;

    /// <summary>
    /// The checkpoint a measurement whose period ends on <paramref name="periodEnd"/> is read against: the first on or
    /// after that day. Null past the last checkpoint, or with no plan, when the KPI's own target applies.
    /// </summary>
    /// <param name="plan">The plan, in any order.</param>
    /// <param name="periodEnd">The last day the measurement describes.</param>
    public static KpiCheckpointValues? For(IEnumerable<KpiCheckpointValues> plan, LocalDate periodEnd)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return plan.Where(c => c.Date >= periodEnd).OrderBy(c => c.Date).FirstOrDefault();
    }

    /// <summary>
    /// The plan as a KPI holds it: labels trimmed, in date order.
    /// </summary>
    public static IReadOnlyList<KpiCheckpointValues> Normalize(IEnumerable<KpiCheckpointValues> plan) =>
        [.. plan.Select(c => c with { Label = c.Label.Trim() }).OrderBy(c => c.Date)];

    /// <summary>
    /// Why this plan cannot be held by a KPI that moves in <paramref name="direction"/>, or null when it can.
    /// </summary>
    /// <remarks>
    /// Planned values are not checked against each other or the target: a seasonal plan may fall back, and a plan may
    /// stop short of the target or run past it.
    /// </remarks>
    public static string? Validate(IReadOnlyCollection<KpiCheckpointValues> plan, KpiTargetDirection direction)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (plan.Count > MaxCheckpoints)
            return KpiErrors.TooManyCheckpoints;

        if (plan.Any(c => c is null))
            return KpiErrors.CheckpointRequired;

        if (plan.Select(c => c.Date).Distinct().Count() != plan.Count)
            return KpiErrors.CheckpointDateTaken;

        foreach (var checkpoint in plan)
        {
            if (string.IsNullOrWhiteSpace(checkpoint.Label))
                return KpiErrors.CheckpointLabelRequired;
            if (checkpoint.Label.Trim().Length > LabelMaxLength)
                return KpiErrors.CheckpointLabelTooLong;
            if (!double.IsFinite(checkpoint.PlannedValue))
                return KpiErrors.NotAFiniteNumber("planned value");

            if (checkpoint.AtRiskValue is not { } atRisk)
                continue;

            if (!double.IsFinite(atRisk))
                return KpiErrors.NotAFiniteNumber("checkpoint at-risk value");

            var error = CheckAtRisk(atRisk, checkpoint.PlannedValue, direction);
            if (error is not null)
                return error;
        }

        return null;
    }

    /// <summary>Why a checkpoint's at-risk value is not short of its planned value, or null when it is.</summary>
    private static string? CheckAtRisk(double atRisk, double planned, KpiTargetDirection direction) => direction switch
    {
        KpiTargetDirection.Increase when atRisk >= planned => KpiErrors.CheckpointAtRiskNotShortIncrease,
        KpiTargetDirection.Decrease when atRisk <= planned => KpiErrors.CheckpointAtRiskNotShortDecrease,
        KpiTargetDirection.Increase or KpiTargetDirection.Decrease => null,
        _ => $"Unsupported target direction: {direction}.",
    };
}
