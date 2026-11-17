using NodaTime;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Domain.Events.Kpis;

/// <summary>
/// A KPI's descriptive fields taken together, as an event records them before and after an edit.
/// </summary>
public sealed record KpiDetails(
    string Name,
    string? Description,
    Guid? CategoryId,
    string? Prefix,
    string? Suffix,
    KpiExpressedAs ExpressedAs);

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
    string? Note);
