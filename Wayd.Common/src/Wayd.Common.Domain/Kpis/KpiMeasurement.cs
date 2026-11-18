using NodaTime;
using Wayd.Common.Domain.Data;
using Wayd.Common.Domain.Events.Kpis;
using Wayd.Common.Extensions;

namespace Wayd.Common.Domain.Kpis;

/// <summary>
/// The raw value of a KPI for one period. Part of the <see cref="Kpi"/> aggregate, changed only through it.
/// </summary>
/// <remarks>
/// Always the raw value, never a percentage derived from it: change from baseline and progress to target are readings
/// worked out from this value, so nobody enters a number Wayd cannot check. A value typed in and a value Wayd computes
/// are the same kind of row, told apart by <see cref="Source"/>.
/// </remarks>
public sealed class KpiMeasurement : BaseAuditableEntity
{
    /// <summary>The longest note a measurement holds.</summary>
    public const int NoteMaxLength = 1024;

    private string? _note;

    private KpiMeasurement() { }

    internal KpiMeasurement(Guid kpiId, KpiMeasurementInput input, KpiPeriod period)
    {
        KpiId = kpiId;
        Apply(input, period);
    }

    /// <summary>The KPI this measurement belongs to.</summary>
    public Guid KpiId { get; private init; }

    /// <summary>The raw measured value.</summary>
    public double Value { get; private set; }

    /// <summary>
    /// The days the value describes, as given for a per-period KPI or worked out from the KPI's window for any other.
    /// </summary>
    public KpiPeriod Period => new(PeriodStart, PeriodEnd);

    /// <summary>The first day of <see cref="Period"/>, mapped so a query can filter on it.</summary>
    public LocalDate PeriodStart { get; private set; }

    /// <summary>The last day of <see cref="Period"/>, mapped so a query can find the latest value.</summary>
    public LocalDate PeriodEnd { get; private set; }

    /// <summary>The kind of record the period covers, such as a sprint, or null when it is a plain date range.</summary>
    public KpiPeriodRecordKind? PeriodRecordKind { get; private set; }

    /// <summary>The sprint, planning interval or release the period covers. Not a foreign key: it points at a different table per kind.</summary>
    public Guid? PeriodRecordId { get; private set; }

    /// <summary>
    /// The moment a point-in-time value describes, or null for any other window. What orders, and tells apart, two
    /// readings on one day.
    /// </summary>
    public Instant? MeasuredAt { get; private set; }

    /// <summary>The day the value was taken, which may be after the period ends.</summary>
    public LocalDate MeasuredOn { get; private set; }

    /// <summary>The employee who took the value, or null when it is not known, as for imported history.</summary>
    public Guid? MeasuredById { get; private set; }

    /// <summary>How the value was arrived at.</summary>
    public KpiMeasurementSource Source { get; private set; }

    /// <summary>How many items the value was computed over, when known, so a parent can recompute a ratio rather than average it.</summary>
    public int? SampleSize { get; private set; }

    /// <summary>Context the person recording it added.</summary>
    public string? Note
    {
        get => _note;
        private set => _note = value.NullIfWhiteSpacePlusTrim();
    }

    /// <summary>Takes the caller's values, with the period the KPI's window worked out for them.</summary>
    internal void Apply(KpiMeasurementInput input, KpiPeriod period)
    {
        Value = input.Value;
        PeriodStart = period.Start;
        PeriodEnd = period.End;
        MeasuredAt = input.Time.At;
        PeriodRecordKind = input.PeriodRecordKind;
        PeriodRecordId = input.PeriodRecordId;
        MeasuredOn = input.MeasuredOn;
        MeasuredById = input.MeasuredById;
        Source = input.Source;
        SampleSize = input.SampleSize;
        Note = input.Note;
    }

    internal KpiMeasurementValues ToValues() =>
        new(Id, Value, Period, PeriodRecordKind, PeriodRecordId, MeasuredOn, MeasuredById, Source, SampleSize, Note, MeasuredAt);
}

/// <summary>
/// What a caller supplies to record or change a measurement.
/// </summary>
/// <param name="Value">The raw measured value.</param>
/// <param name="Time">When it was taken: a period, an as-of day or a moment, as the KPI's window takes it.</param>
/// <param name="PeriodRecordKind">The kind of record the period covers, if any; requires <paramref name="PeriodRecordId"/>.</param>
/// <param name="PeriodRecordId">The record the period covers, if any; requires <paramref name="PeriodRecordKind"/>.</param>
/// <param name="MeasuredOn">The day the value was taken.</param>
/// <param name="MeasuredById">The employee who took it, when known.</param>
/// <param name="Source">How the value was arrived at.</param>
/// <param name="SampleSize">How many items the value was computed over, when known.</param>
/// <param name="Note">Context for the value.</param>
public sealed record KpiMeasurementInput(
    double Value,
    KpiMeasurementTime Time,
    KpiPeriodRecordKind? PeriodRecordKind,
    Guid? PeriodRecordId,
    LocalDate MeasuredOn,
    Guid? MeasuredById,
    KpiMeasurementSource Source,
    int? SampleSize,
    string? Note)
{
    /// <summary>Why this input cannot be recorded, or null when it can.</summary>
    internal string? Validate()
    {
        if (!double.IsFinite(Value))
            return KpiErrors.NotAFiniteNumber("measured value");

        if (Time is null)
            return KpiErrors.PeriodRequired;

        if (PeriodRecordKind.HasValue != PeriodRecordId.HasValue || PeriodRecordId == Guid.Empty)
            return KpiErrors.PeriodRecordIncomplete;

        if (PeriodRecordKind is { } kind && !Enum.IsDefined(kind))
            return $"Unsupported period record kind: {kind}.";

        if (!Enum.IsDefined(Source))
            return $"Unsupported measurement source: {Source}.";

        if (SampleSize < 0)
            return KpiErrors.SampleSizeNegative;

        if (Note is { Length: > KpiMeasurement.NoteMaxLength })
            return $"A note can be at most {KpiMeasurement.NoteMaxLength} characters.";

        return null;
    }
}
