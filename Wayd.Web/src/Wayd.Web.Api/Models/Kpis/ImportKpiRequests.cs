using NodaTime.Text;
using Wayd.Common.Application.Kpis.Imports;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Kpis;
using Wayd.Common.Extensions;

namespace Wayd.Web.Api.Models.Kpis;

/// <summary>
/// A single CSV row for the KPI import — one KPI on an existing record, without its history. Measurements arrive
/// in a second file and point back here by this row's <c>ImportId</c>.
/// </summary>
public sealed class ImportKpiRequest
{
    /// <summary>
    /// The caller's own key for this row, unique within the file (case-insensitively). Results are reported against
    /// it, and the measurements file names it to say which KPI a value belongs to. Leave a cell blank and the row is
    /// known by its position instead, but the column itself must be in the file.
    /// </summary>
    public string? ImportId { get; set; }

    /// <summary>The kind of record the KPI goes on: `organization.team` for a team or a team of teams.</summary>
    public string SubjectType { get; set; } = "organization.team";

    /// <summary>The record the KPI goes on, by id.</summary>
    public Guid SubjectId { get; set; }

    /// <summary>What the KPI is called, up to 128 characters.</summary>
    public string Name { get; set; } = default!;

    /// <summary>What it measures and why, up to 1,024 characters.</summary>
    public string? Description { get; set; }

    /// <summary>An active KPI category, by id, or blank for none.</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>A symbol shown before a value, such as `$`.</summary>
    public string? Prefix { get; set; }

    /// <summary>A unit shown after a value, such as `days`.</summary>
    public string? Suffix { get; set; }

    /// <summary>Which reading the scorecard shows. `Value` when blank.</summary>
    [CsvValues(typeof(KpiExpressedAs))]
    public string? ExpressedAs { get; set; }

    /// <summary>Whether the KPI should go up or down: `Increase` or `Decrease`.</summary>
    [CsvValues(typeof(KpiTargetDirection))]
    public string Direction { get; set; } = default!;

    /// <summary>Where it started. Required for a `RelativeChange` target.</summary>
    public double? BaselineValue { get; set; }

    /// <summary>The first day the baseline was taken over. Give both ends of the window or neither.</summary>
    public DateOnly? BaselineWindowStart { get; set; }

    /// <summary>The last day the baseline was taken over.</summary>
    public DateOnly? BaselineWindowEnd { get; set; }

    /// <summary>`Absolute` for a value to reach, or `RelativeChange` for a percentage change from the baseline. `Absolute` when blank.</summary>
    [CsvValues(typeof(KpiTargetKind))]
    public string? TargetKind { get; set; }

    /// <summary>The value to reach, or the percentage change — 25 for 25%.</summary>
    public double TargetValue { get; set; }

    /// <summary>How an at-risk threshold is stated: `Absolute` or `ProgressToTarget`. Blank for none.</summary>
    [CsvValues(typeof(KpiAtRiskKind))]
    public string? AtRiskKind { get; set; }

    /// <summary>The threshold value, or the percentage of the way to the target.</summary>
    public double? AtRiskValue { get; set; }

    /// <summary>`true` to import the KPI closed, as history only. Open when blank.</summary>
    public bool? IsClosed { get; set; }

    /// <summary>
    /// What span each measurement covers: `PerPeriod` (each gives its own period), `Cumulative` (from
    /// `WindowAnchorDate` to its as-of day), `Rolling` (`WindowRollingCount` `WindowRollingUnit`s ending on its as-of
    /// day) or `PointInTime` (the state at one moment). `PerPeriod` when blank.
    /// </summary>
    [CsvValues(typeof(KpiWindowKind))]
    public string? WindowKind { get; set; }

    /// <summary>For `Cumulative` only: the first day every measurement covers.</summary>
    public DateOnly? WindowAnchorDate { get; set; }

    /// <summary>For `Rolling` only: how many units each measurement covers.</summary>
    public int? WindowRollingCount { get; set; }

    /// <summary>For `Rolling` only: what the count counts — `Days`.</summary>
    [CsvValues(typeof(KpiRollingUnit))]
    public string? WindowRollingUnit { get; set; }

    /// <summary>For `PerPeriod` only: read the KPI as the average of this many latest measurements, 2 to 12. Blank for the latest alone.</summary>
    public int? RollingAverageOf { get; set; }

    public ImportKpiDto ToImportKpiDto(IReadOnlyList<ImportKpiMeasurementDto> measurements) => new(
        SubjectType.Trim(),
        SubjectId,
        Name,
        Description,
        CategoryId,
        Prefix,
        Suffix,
        ParseOr(ExpressedAs, KpiExpressedAs.Value),
        Enum.Parse<KpiTargetDirection>(Direction.Trim(), ignoreCase: true),
        new KpiBaseline(
            BaselineValue,
            BaselineWindowStart is { } start && BaselineWindowEnd is { } end && end >= start
                ? new KpiPeriod(start.ToLocalDate(), end.ToLocalDate())
                : null),
        new KpiTarget(ParseOr(TargetKind, KpiTargetKind.Absolute), TargetValue),
        string.IsNullOrWhiteSpace(AtRiskKind) || AtRiskValue is null
            ? null
            : new KpiAtRiskThreshold(Enum.Parse<KpiAtRiskKind>(AtRiskKind.Trim(), ignoreCase: true), AtRiskValue.Value),
        IsClosed ?? false,
        measurements,
        new KpiMeasurementWindow(
            ParseOr(WindowKind, KpiWindowKind.PerPeriod),
            WindowAnchorDate?.ToLocalDate(),
            WindowRollingCount,
            string.IsNullOrWhiteSpace(WindowRollingUnit) ? null : Enum.Parse<KpiRollingUnit>(WindowRollingUnit.Trim(), ignoreCase: true)),
        RollingAverageOf);

    private static T ParseOr<T>(string? value, T fallback) where T : struct, Enum =>
        string.IsNullOrWhiteSpace(value) ? fallback : Enum.Parse<T>(value.Trim(), ignoreCase: true);
}

/// <summary>Rejects a KPI row whose columns cannot be read before anything is submitted.</summary>
public sealed class ImportKpiRequestValidator : CustomValidator<ImportKpiRequest>
{
    public ImportKpiRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(k => k.SubjectType).NotEmpty().MaximumLength(KpiSubjectTypeDescriptor.KeyMaxLength);
        RuleFor(k => k.SubjectId).NotEmpty();
        RuleFor(k => k.Name).NotEmpty().MaximumLength(Kpi.NameMaxLength);
        RuleFor(k => k.Description).MaximumLength(Kpi.DescriptionMaxLength);
        RuleFor(k => k.Prefix).MaximumLength(Kpi.AffixMaxLength);
        RuleFor(k => k.Suffix).MaximumLength(Kpi.AffixMaxLength);

        RuleFor(k => k.Direction)
            .NotEmpty()
            .Must(IsOne<KpiTargetDirection>)
                .WithMessage("Direction must be either 'Increase' or 'Decrease'.");

        RuleFor(k => k.ExpressedAs)
            .Must(IsOne<KpiExpressedAs>)
                .When(k => !string.IsNullOrWhiteSpace(k.ExpressedAs))
                .WithMessage("ExpressedAs must be 'Value', 'ChangeFromBaseline' or 'ProgressToTarget'.");

        RuleFor(k => k.TargetKind)
            .Must(IsOne<KpiTargetKind>)
                .When(k => !string.IsNullOrWhiteSpace(k.TargetKind))
                .WithMessage("TargetKind must be either 'Absolute' or 'RelativeChange'.");

        RuleFor(k => k.AtRiskKind)
            .Must(IsOne<KpiAtRiskKind>)
                .When(k => !string.IsNullOrWhiteSpace(k.AtRiskKind))
                .WithMessage("AtRiskKind must be either 'Absolute' or 'ProgressToTarget'.");

        RuleFor(k => k.AtRiskValue)
            .NotNull()
                .When(k => !string.IsNullOrWhiteSpace(k.AtRiskKind))
                .WithMessage("An AtRiskKind needs an AtRiskValue.");

        RuleFor(k => k.AtRiskKind)
            .NotEmpty()
                .When(k => k.AtRiskValue is not null)
                .WithMessage("An AtRiskValue needs an AtRiskKind: 'Absolute' or 'ProgressToTarget'.");

        RuleFor(k => k.BaselineWindowEnd)
            .NotNull()
                .When(k => k.BaselineWindowStart is not null, ApplyConditionTo.CurrentValidator)
                .WithMessage("A baseline window needs both a start and an end.")
            .GreaterThanOrEqualTo(k => k.BaselineWindowStart!.Value)
                .When(k => k.BaselineWindowStart is not null && k.BaselineWindowEnd is not null, ApplyConditionTo.CurrentValidator)
                .WithMessage(KpiErrors.PeriodEndsBeforeStart);

        RuleFor(k => k.BaselineWindowStart)
            .NotNull()
                .When(k => k.BaselineWindowEnd is not null)
                .WithMessage("A baseline window needs both a start and an end.");

        RuleFor(k => k.WindowKind)
            .Must(IsOne<KpiWindowKind>)
                .When(k => !string.IsNullOrWhiteSpace(k.WindowKind))
                .WithMessage("WindowKind must be 'PerPeriod', 'Cumulative', 'Rolling' or 'PointInTime'.");

        RuleFor(k => k.WindowRollingUnit)
            .Must(IsOne<KpiRollingUnit>)
                .When(k => !string.IsNullOrWhiteSpace(k.WindowRollingUnit))
                .WithMessage("WindowRollingUnit must be 'Days'.");

        RuleFor(k => k.RollingAverageOf)
            .InclusiveBetween(Kpi.MinRollingAverageOf, Kpi.MaxRollingAverageOf)
                .When(k => k.RollingAverageOf is not null);
    }

    private static bool IsOne<T>(string? value) where T : struct, Enum =>
        Enum.TryParse<T>(value?.Trim(), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed);
}

/// <summary>
/// A single CSV row for the measurements file: one value of one KPI, saying when it was taken the one way the KPI's
/// window takes — `PeriodStart` and `PeriodEnd`, `AsOf`, or `MeasuredAt`.
/// </summary>
public sealed class ImportKpiMeasurementRequest
{
    /// <summary>The `ImportId` of the KPI row this value belongs to.</summary>
    public string KpiImportId { get; set; } = default!;

    /// <summary>For a `PerPeriod` KPI: the first day the value describes.</summary>
    public DateOnly? PeriodStart { get; set; }

    /// <summary>For a `PerPeriod` KPI: the last day the value describes. A KPI's periods cannot overlap.</summary>
    public DateOnly? PeriodEnd { get; set; }

    /// <summary>For a `Cumulative` or `Rolling` KPI: the last day the value covers. One value per day.</summary>
    public DateOnly? AsOf { get; set; }

    /// <summary>
    /// For a `PointInTime` KPI: the moment the value describes, as ISO 8601 with its offset, such as
    /// `2026-03-06T15:00:00-06:00` or `2026-03-06T21:00:00Z`. A time without an offset is refused, since it names no
    /// single moment. One value per moment.
    /// </summary>
    public string? MeasuredAt { get; set; }

    /// <summary>
    /// The ways a <see cref="MeasuredAt"/> may be written: with or without seconds, the offset as <c>Z</c> or
    /// <c>±hh:mm</c>. Every one carries an offset, so a time is never read in the server's own zone.
    /// </summary>
    private static readonly OffsetDateTimePattern[] MomentPatterns =
    [
        OffsetDateTimePattern.ExtendedIso,
        OffsetDateTimePattern.CreateWithInvariantCulture("uuuu'-'MM'-'dd'T'HH':'mmo<G>"),
    ];

    /// <summary><see cref="MeasuredAt"/> as a moment, or null when it is blank or cannot be read as one.</summary>
    public Instant? ParseMeasuredAt()
    {
        if (string.IsNullOrWhiteSpace(MeasuredAt))
            return null;

        foreach (var pattern in MomentPatterns)
        {
            var parsed = pattern.Parse(MeasuredAt.Trim());
            if (parsed.Success)
                return parsed.Value.ToInstant();
        }

        return null;
    }

    /// <summary>The raw measured value — never a percentage worked out from it.</summary>
    public double Value { get; set; }

    /// <summary>The day the value was taken; the last day it covers when blank.</summary>
    public DateOnly? MeasuredOn { get; set; }

    /// <summary>How many items the value was computed over, when known.</summary>
    public int? SampleSize { get; set; }

    /// <summary>Context for the value, up to 1,024 characters.</summary>
    public string? Note { get; set; }

    public ImportKpiMeasurementDto ToImportKpiMeasurementDto() => new(
        Value,
        PeriodStart?.ToLocalDate(),
        PeriodEnd?.ToLocalDate(),
        MeasuredOn?.ToLocalDate(),
        SampleSize,
        Note,
        AsOf?.ToLocalDate(),
        ParseMeasuredAt());

    /// <summary>When the value was taken, as an error message names it.</summary>
    public string Describe() =>
        !string.IsNullOrWhiteSpace(MeasuredAt) ? $"At: {MeasuredAt.Trim()}"
        : AsOf is { } asOf ? FormattableString.Invariant($"As of: {asOf:yyyy-MM-dd}")
        : FormattableString.Invariant($"Period: {PeriodStart:yyyy-MM-dd} to {PeriodEnd:yyyy-MM-dd}");
}

/// <summary>Rejects a measurement row whose columns cannot be read before anything is submitted.</summary>
public sealed class ImportKpiMeasurementRequestValidator : CustomValidator<ImportKpiMeasurementRequest>
{
    public ImportKpiMeasurementRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(m => m.KpiImportId).NotEmpty();
        RuleFor(m => m)
            .Must(m => (m.PeriodStart is not null || m.PeriodEnd is not null ? 1 : 0) + (m.AsOf is not null ? 1 : 0) + (!string.IsNullOrWhiteSpace(m.MeasuredAt) ? 1 : 0) == 1)
                .WithName("Measurement")
                .WithMessage("Give exactly one of PeriodStart and PeriodEnd, AsOf, or MeasuredAt, as the KPI's window takes it.");
        RuleFor(m => m.MeasuredAt)
            .Must((m, _) => m.ParseMeasuredAt() is not null)
                .When(m => !string.IsNullOrWhiteSpace(m.MeasuredAt))
                .WithMessage("MeasuredAt must be an ISO 8601 moment with its offset, such as 2026-03-06T15:00:00-06:00 or 2026-03-06T21:00:00Z.");
        RuleFor(m => m.PeriodEnd)
            .NotNull()
                .When(m => m.PeriodStart is not null, ApplyConditionTo.CurrentValidator)
                .WithMessage("A period needs both PeriodStart and PeriodEnd.")
            .GreaterThanOrEqualTo(m => m.PeriodStart!.Value)
                .When(m => m.PeriodStart is not null && m.PeriodEnd is not null, ApplyConditionTo.CurrentValidator)
                .WithMessage(KpiErrors.PeriodEndsBeforeStart);
        RuleFor(m => m.PeriodStart)
            .NotNull()
                .When(m => m.PeriodEnd is not null)
                .WithMessage("A period needs both PeriodStart and PeriodEnd.");
        RuleFor(m => m.SampleSize).GreaterThanOrEqualTo(0).When(m => m.SampleSize is not null);
        RuleFor(m => m.Note).MaximumLength(KpiMeasurement.NoteMaxLength);
    }
}
