using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Kpis.Imports;

/// <summary>
/// One KPI row of the import, with the measurements its history file assigned to it.
/// </summary>
/// <remarks>
/// The measurements travel on the KPI's row because they are part of its aggregate: a KPI and its history
/// apply together or not at all, so a period that overlaps another rejects the KPI it belongs to rather than
/// leaving it half-imported.
/// </remarks>
/// <param name="SubjectType">The registered subject type, such as <c>organization.team</c>.</param>
/// <param name="SubjectId">The record the KPI goes on, by id.</param>
/// <param name="Name">What the KPI is called.</param>
/// <param name="Description">What it measures and why.</param>
/// <param name="CategoryId">An active category, by id, or null.</param>
/// <param name="Prefix">A symbol shown before a value.</param>
/// <param name="Suffix">A unit shown after a value.</param>
/// <param name="ExpressedAs">Which reading the scorecard shows.</param>
/// <param name="Direction">Whether it should go up or down.</param>
/// <param name="Baseline">Where it started.</param>
/// <param name="Target">What it aims for.</param>
/// <param name="AtRisk">The optional at-risk threshold.</param>
/// <param name="IsClosed">Whether it arrives closed, as history only.</param>
/// <param name="Measurements">Its history, in any order.</param>
/// <param name="Window">How its measurements are windowed; per period when null, as every payload queued before windows existed means.</param>
/// <param name="RollingAverageOf">How many of the latest measurements its reading averages, or null for the latest alone.</param>
public sealed record ImportKpiDto(
    string SubjectType,
    Guid SubjectId,
    string Name,
    string? Description,
    Guid? CategoryId,
    string? Prefix,
    string? Suffix,
    KpiExpressedAs ExpressedAs,
    KpiTargetDirection Direction,
    KpiBaseline Baseline,
    KpiTarget Target,
    KpiAtRiskThreshold? AtRisk,
    bool IsClosed,
    IReadOnlyList<ImportKpiMeasurementDto> Measurements,
    KpiMeasurementWindow? Window = null,
    int? RollingAverageOf = null);

/// <summary>
/// One measurement of an imported KPI, saying when it was taken the one way its KPI's window takes: a period, an
/// as-of day or a moment.
/// </summary>
/// <param name="Value">The raw measured value.</param>
/// <param name="PeriodStart">The first day it describes, for a per-period KPI.</param>
/// <param name="PeriodEnd">The last day it describes, for a per-period KPI.</param>
/// <param name="MeasuredOn">The day it was taken; when not given, the last day it covers.</param>
/// <param name="SampleSize">How many items it was computed over, when known.</param>
/// <param name="Note">Context for it.</param>
/// <param name="AsOf">The last day it covers, for a cumulative or rolling KPI.</param>
/// <param name="MeasuredAt">The moment it describes, for a point-in-time KPI.</param>
public sealed record ImportKpiMeasurementDto(
    double Value,
    LocalDate? PeriodStart,
    LocalDate? PeriodEnd,
    LocalDate? MeasuredOn,
    int? SampleSize,
    string? Note,
    LocalDate? AsOf = null,
    Instant? MeasuredAt = null)
{
    /// <summary>When it was taken, as the KPI's window will judge it.</summary>
    public KpiMeasurementTime Time() => new(
        PeriodStart is { } start && PeriodEnd is { } end && end >= start ? new KpiPeriod(start, end) : null,
        AsOf,
        MeasuredAt);

    /// <summary>The last day it covers, for ordering and messages: the period's end, the as-of day, or the moment's day.</summary>
    public LocalDate? LastDay(DateTimeZone zone) => PeriodEnd ?? AsOf ?? MeasuredAt?.InZone(zone).Date;

    /// <summary>When it was taken, as a person reading an error would name it.</summary>
    public string Describe() =>
        MeasuredAt is { } at ? FormattableString.Invariant($"at {at}")
        : AsOf is { } asOf ? FormattableString.Invariant($"as of {asOf:yyyy-MM-dd}")
        : FormattableString.Invariant($"{PeriodStart:yyyy-MM-dd} to {PeriodEnd:yyyy-MM-dd}");
}

/// <summary>The shape a KPI row must have before it reaches the run. The domain checks the goal itself.</summary>
public sealed class ImportKpiDtoValidator : AbstractValidator<ImportKpiDto>
{
    public ImportKpiDtoValidator()
    {
        RuleFor(k => k.SubjectType).NotEmpty().MaximumLength(KpiSubjectTypeDescriptor.KeyMaxLength);
        RuleFor(k => k.SubjectId).NotEmpty();
        RuleFor(k => k.Name).NotEmpty().MaximumLength(Kpi.NameMaxLength);
        RuleFor(k => k.Description).MaximumLength(Kpi.DescriptionMaxLength);
        RuleFor(k => k.Prefix).MaximumLength(Kpi.AffixMaxLength);
        RuleFor(k => k.Suffix).MaximumLength(Kpi.AffixMaxLength);
        RuleFor(k => k.Baseline).NotNull();
        RuleFor(k => k.Target).NotNull();
        RuleFor(k => k.Measurements).NotNull();
        RuleFor(k => k.RollingAverageOf).InclusiveBetween(Kpi.MinRollingAverageOf, Kpi.MaxRollingAverageOf).When(k => k.RollingAverageOf is not null);
        RuleForEach(k => k.Measurements).ChildRules(m =>
        {
            m.RuleFor(x => x.PeriodEnd).GreaterThanOrEqualTo(x => x.PeriodStart).WithMessage(KpiErrors.PeriodEndsBeforeStart)
                .When(x => x.PeriodStart is not null && x.PeriodEnd is not null);
            m.RuleFor(x => x)
                .Must(x => (x.PeriodStart is not null && x.PeriodEnd is not null ? 1 : 0) + (x.AsOf is not null ? 1 : 0) + (x.MeasuredAt is not null ? 1 : 0) == 1)
                .WithName("Measurement")
                .WithMessage("A measurement needs exactly one of a period, an as-of date or a moment.");
            m.RuleFor(x => x.SampleSize).GreaterThanOrEqualTo(0).When(x => x.SampleSize is not null);
            m.RuleFor(x => x.Note).MaximumLength(KpiMeasurement.NoteMaxLength);
        });
    }
}
