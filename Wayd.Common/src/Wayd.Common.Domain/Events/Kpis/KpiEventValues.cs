using NodaTime;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Domain.Events.Kpis;

/// <summary>
/// A KPI's descriptive fields taken together, as an event records them before and after an edit.
/// </summary>
/// <param name="Name">What the KPI is called.</param>
/// <param name="Description">What it measures and why.</param>
/// <param name="CategoryId">The category it is grouped under, or null.</param>
/// <param name="Prefix">A symbol shown before a value.</param>
/// <param name="Suffix">A unit shown after a value.</param>
/// <param name="ExpressedAs">Which reading the scorecard shows.</param>
/// <param name="RollingAverageOf">
/// How many of the latest measurements the reading averages, or null to read the latest alone. Null in events
/// recorded before the rolling average existed, which is what they meant.
/// </param>
public sealed record KpiDetails(
    string Name,
    string? Description,
    Guid? CategoryId,
    string? Prefix,
    string? Suffix,
    KpiExpressedAs ExpressedAs,
    int? RollingAverageOf = null);

/// <summary>
/// One measurement as an event records it: when it was added, before and after a change, or when it was removed.
/// </summary>
/// <param name="MeasurementId">The measurement's id.</param>
/// <param name="Value">The raw measured value.</param>
/// <param name="Period">The days the value describes.</param>
/// <param name="PeriodRecordKind">The kind of record the period covers, if any.</param>
/// <param name="PeriodRecordId">The sprint, planning interval or release the period covers, if any.</param>
/// <param name="MeasuredOn">The day the value was taken.</param>
/// <param name="MeasuredById">The employee who took it, when known.</param>
/// <param name="Source">How the value was arrived at.</param>
/// <param name="SampleSize">How many items the value was computed over, when known.</param>
/// <param name="Note">Context the person recording it added.</param>
/// <param name="MeasuredAt">
/// The moment a point-in-time value describes, or null for any other window. Null in events recorded before
/// point-in-time KPIs existed, which is what they meant.
/// </param>
public sealed record KpiMeasurementValues(
    Guid MeasurementId,
    double Value,
    KpiPeriod Period,
    KpiPeriodRecordKind? PeriodRecordKind,
    Guid? PeriodRecordId,
    LocalDate MeasuredOn,
    Guid? MeasuredById,
    KpiMeasurementSource Source,
    int? SampleSize,
    string? Note,
    Instant? MeasuredAt = null);
