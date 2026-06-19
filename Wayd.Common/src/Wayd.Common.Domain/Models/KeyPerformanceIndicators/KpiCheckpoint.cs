using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Data;
using NodaTime;

namespace Wayd.Common.Domain.Models.KeyPerformanceIndicators;

/// <summary>
/// Represents the common properties and behavior for a KPI checkpoint (expected progress checkpoint).
/// </summary>
public abstract class KpiCheckpoint : BaseAuditableEntity
{
    protected string _dateLabel = default!;

    protected KpiCheckpoint() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="KpiCheckpoint"/> class.
    /// </summary>
    /// <param name="kpiId">The unique identifier of the parent KPI.</param>
    /// <param name="targetValue">The planned target value for the KPI at this checkpoint.</param>
    /// <param name="checkpointDate">The date and time when this planned target is expected to be achieved.</param>
    /// <param name="dateLabel">A short label that describes the date of this checkpoint.</param>
    /// <param name="atRiskValue">The optional at-risk threshold value for this checkpoint.</param>
    protected KpiCheckpoint(Guid kpiId, double targetValue, Instant checkpointDate, string dateLabel, double? atRiskValue = null)
    {
        KpiId = kpiId;
        TargetValue = targetValue;
        CheckpointDate = checkpointDate;
        DateLabel = dateLabel;
        AtRiskValue = atRiskValue;
    }

    /// <summary>
    /// The unique identifier of the parent KPI.
    /// </summary>
    public Guid KpiId { get; protected init; }

    /// <summary>
    /// The planned target value that the KPI should reach by the checkpoint date.
    /// </summary>
    public double TargetValue { get; protected set; }

    /// <summary>
    /// The optional at-risk threshold value for this checkpoint. When set, enables 3-zone health assessment:
    /// Healthy (actual met target), AtRisk (actual between AtRiskValue and TargetValue), Unhealthy (actual missed AtRiskValue).
    /// </summary>
    public double? AtRiskValue { get; protected set; }

    /// <summary>
    /// The date and time when the planned target is expected to be achieved.
    /// </summary>
    public Instant CheckpointDate { get; protected set; }

    /// <summary>
    /// A short label that describes the date of this checkpoint. For example, "Q1" or "Jan".
    /// </summary>
    public string DateLabel
    {
        get => _dateLabel;
        protected set => _dateLabel = Guard.Against.NullOrWhiteSpace(value, nameof(DateLabel)).Trim();
    }

    /// <summary>
    /// Updates the target value, checkpoint date, date label, and optional at-risk value for this KPI checkpoint.
    /// </summary>
    /// <param name="targetValue"></param>
    /// <param name="checkpointDate"></param>
    /// <param name="dateLabel"></param>
    /// <param name="atRiskValue"></param>
    /// <returns></returns>
    public virtual Result Update(double targetValue, Instant checkpointDate, string dateLabel, double? atRiskValue = null)
    {
        TargetValue = targetValue;
        CheckpointDate = checkpointDate;
        DateLabel = dateLabel;
        AtRiskValue = atRiskValue;

        return Result.Success();
    }
}
