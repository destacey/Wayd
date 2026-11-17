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
        measurements);

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
                .When(k => k.BaselineWindowStart is not null)
                .WithMessage("A baseline window needs both a start and an end.")
            .GreaterThanOrEqualTo(k => k.BaselineWindowStart!.Value)
                .When(k => k.BaselineWindowStart is not null && k.BaselineWindowEnd is not null)
                .WithMessage(KpiErrors.PeriodEndsBeforeStart);

        RuleFor(k => k.BaselineWindowStart)
            .NotNull()
                .When(k => k.BaselineWindowEnd is not null)
                .WithMessage("A baseline window needs both a start and an end.");
    }

    private static bool IsOne<T>(string? value) where T : struct, Enum =>
        Enum.TryParse<T>(value?.Trim(), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed);
}

/// <summary>
/// A single CSV row for the measurements file: one value of one KPI for one period.
/// </summary>
public sealed class ImportKpiMeasurementRequest
{
    /// <summary>The `ImportId` of the KPI row this value belongs to.</summary>
    public string KpiImportId { get; set; } = default!;

    /// <summary>The first day the value describes.</summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>The last day the value describes. A KPI's periods cannot overlap.</summary>
    public DateOnly PeriodEnd { get; set; }

    /// <summary>The raw measured value — never a percentage worked out from it.</summary>
    public double Value { get; set; }

    /// <summary>The day the value was taken; the period's last day when blank.</summary>
    public DateOnly? MeasuredOn { get; set; }

    /// <summary>How many items the value was computed over, when known.</summary>
    public int? SampleSize { get; set; }

    /// <summary>Context for the value, up to 1,024 characters.</summary>
    public string? Note { get; set; }

    public ImportKpiMeasurementDto ToImportKpiMeasurementDto() => new(
        Value, PeriodStart.ToLocalDate(), PeriodEnd.ToLocalDate(), MeasuredOn?.ToLocalDate(), SampleSize, Note);
}

/// <summary>Rejects a measurement row whose columns cannot be read before anything is submitted.</summary>
public sealed class ImportKpiMeasurementRequestValidator : CustomValidator<ImportKpiMeasurementRequest>
{
    public ImportKpiMeasurementRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(m => m.KpiImportId).NotEmpty();
        RuleFor(m => m.PeriodEnd).GreaterThanOrEqualTo(m => m.PeriodStart).WithMessage(KpiErrors.PeriodEndsBeforeStart);
        RuleFor(m => m.SampleSize).GreaterThanOrEqualTo(0).When(m => m.SampleSize is not null);
        RuleFor(m => m.Note).MaximumLength(KpiMeasurement.NoteMaxLength);
    }
}
