using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Data;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events.Kpis;
using Wayd.Common.Extensions;

namespace Wayd.Common.Domain.Kpis;

/// <summary>
/// A key performance indicator on one record — a team, say — with what it is measured against and the measurements
/// taken over time.
/// </summary>
/// <remarks>
/// <strong>It knows nothing about its record.</strong> The subject is a registered type and an id; who may read or
/// change it is the module's subject type's answer, asked before any method here is called. <see cref="SubjectId"/>
/// is deliberately not a foreign key: it points at a different table per subject type.
/// <para>
/// Its measurements are part of the aggregate and are loaded with it, so the rule that no two periods overlap is
/// checked here. A scorecard never loads them: it reads the latest two values in its query.
/// </para>
/// <para>
/// Not <c>Models.KeyPerformanceIndicators.Kpi</c>, the base strategic initiative KPIs derive from, which has no
/// subject, no measurement periods and no target rule.
/// </para>
/// </remarks>
public sealed class Kpi : BaseAuditableEntity, IHasIdAndKey
{
    /// <summary>The longest name a KPI holds.</summary>
    public const int NameMaxLength = 128;

    /// <summary>The longest description a KPI holds.</summary>
    public const int DescriptionMaxLength = 1024;

    /// <summary>The longest prefix or suffix a KPI holds.</summary>
    public const int AffixMaxLength = 16;

    /// <summary>The fewest measurements a rolling average is taken over; one is the latest value itself.</summary>
    public const int MinRollingAverageOf = 2;

    /// <summary>
    /// The most measurements a rolling average is taken over: a year of monthly values, or a long run of sprints. A
    /// scorecard reads this many plus one per KPI, to show the average and the one before it.
    /// </summary>
    public const int MaxRollingAverageOf = 12;

    private readonly List<KpiMeasurement> _measurements = [];
    // Null until loaded: EF creates the list when the query includes the plan, even an empty one, and leaves it null
    // otherwise. That is what lets the plan's readers refuse a KPI loaded without it rather than read it as no plan.
    private List<KpiCheckpoint>? _checkpoints;

    private string _name = default!;
    private string? _description;
    private string? _prefix;
    private string? _suffix;

    // The goal's parts as columns. EF cannot build a record whose constructor takes another value object, so the
    // records are read from these rather than mapped.
    private double? _baselineValue;
    private LocalDate? _baselineWindowStart;
    private LocalDate? _baselineWindowEnd;
    private KpiTargetKind _targetKind;
    private double _targetValue;
    private KpiAtRiskKind? _atRiskKind;
    private double? _atRiskValue;

    // The window's parts as columns, for the same reason as the goal's.
    private KpiWindowKind _windowKind = KpiWindowKind.PerPeriod;
    private LocalDate? _windowAnchorDate;
    private int? _windowRollingCount;
    private KpiRollingUnit? _windowRollingUnit;

    private Kpi() { }

    private Kpi(string subjectType, Guid subjectId, KpiDefinition definition, int order)
    {
        _checkpoints = [];
        SubjectType = subjectType;
        SubjectId = subjectId;
        ApplyWindow(definition.WindowOrDefault);
        ApplyDetails(definition);
        ApplyGoal(definition.Goal);
        Source = KpiSource.Manual;
        Status = KpiStatus.Active;
        Order = order;
    }

    /// <summary>
    /// Adds a KPI to a record, failing when the subject type is not registered or the definition does not hold.
    /// </summary>
    /// <remarks>
    /// Says nothing about whether the actor may change the record's KPIs. That is the subject type's answer, asked
    /// first. The category is not checked either: whether it exists and is active is the caller's to confirm.
    /// </remarks>
    public static Result<Kpi> Create(string subjectType, Guid subjectId, KpiDefinition definition, int order, EventActor actor, Instant timestamp)
    {
        var descriptor = KpiSubjectTypes.Resolve(subjectType);
        if (descriptor.IsFailure)
            return Result.Failure<Kpi>(descriptor.Error);

        Guard.Against.Default(subjectId, nameof(subjectId));
        Guard.Against.Null(definition, nameof(definition));
        Guard.Against.Null(actor, nameof(actor));

        var error = definition.Validate() ?? CheckRollingAverage(definition.RollingAverageOf, definition.WindowOrDefault);
        if (error is not null)
            return Result.Failure<Kpi>(error);

        // The registered key rather than the caller's spelling, so a KPI is stored under one casing.
        var kpi = new Kpi(descriptor.Value.Key, subjectId, definition, order);

        var id = kpi.Id;
        var details = kpi.Details;
        var goal = kpi.Goal;
        var source = kpi.Source;
        var window = kpi.Window;
        var type = kpi.SubjectType;
        kpi.AddKeyedDomainEvent(() => new KpiAddedEvent(id, kpi.Key, type, subjectId, details, goal, source, order, actor, timestamp, window));

        return kpi;
    }

    /// <summary>The unique key of the KPI. An alternate key to the Id.</summary>
    public int Key { get; private init; }

    /// <summary>The registered subject type: what kind of record the KPI is about.</summary>
    public string SubjectType { get; private init; } = default!;

    /// <summary>The record the KPI is about, unique only within <see cref="SubjectType"/>.</summary>
    public Guid SubjectId { get; private init; }

    /// <summary>What the KPI is called on the scorecard.</summary>
    public string Name
    {
        get => _name;
        private set => _name = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    }

    /// <summary>What the KPI measures and why it matters.</summary>
    public string? Description
    {
        get => _description;
        private set => _description = value.NullIfWhiteSpacePlusTrim();
    }

    /// <summary>The organization-defined category the scorecard groups it under, or null when uncategorized.</summary>
    public Guid? CategoryId { get; private set; }

    /// <summary>A symbol shown before a value, such as "$".</summary>
    public string? Prefix
    {
        get => _prefix;
        private set => _prefix = value.NullIfWhiteSpacePlusTrim();
    }

    /// <summary>A unit shown after a value, such as "days" or "%".</summary>
    public string? Suffix
    {
        get => _suffix;
        private set => _suffix = value.NullIfWhiteSpacePlusTrim();
    }

    /// <summary>Whether the KPI should go up or down.</summary>
    public KpiTargetDirection Direction { get; private set; }

    /// <summary>Where the KPI started.</summary>
    public KpiBaseline Baseline =>
        new(_baselineValue, _baselineWindowStart is { } start && _baselineWindowEnd is { } end ? new KpiPeriod(start, end) : null);

    /// <summary>What it aims for, as entered.</summary>
    public KpiTarget Target => new(_targetKind, _targetValue);

    /// <summary>The optional line short of the target past which a measurement is at risk.</summary>
    public KpiAtRiskThreshold? AtRisk => _atRiskKind is { } kind ? new KpiAtRiskThreshold(kind, _atRiskValue ?? 0) : null;

    /// <summary>Where its measurements come from.</summary>
    public KpiSource Source { get; private set; }

    /// <summary>Which reading of the raw value the scorecard shows.</summary>
    public KpiExpressedAs ExpressedAs { get; private set; }

    /// <summary>
    /// How many of the latest measurements the reading averages, or null to read the latest alone. Only on a per-period
    /// KPI, for values that add up per period, such as velocity read over the last 7 sprints.
    /// </summary>
    /// <remarks>
    /// A reading, never stored on a measurement, so changing it reads through the whole history at once.
    /// </remarks>
    public int? RollingAverageOf { get; private set; }

    /// <summary>How its measurements are windowed: which span of time each value describes.</summary>
    public KpiMeasurementWindow Window => new(_windowKind, _windowAnchorDate, _windowRollingCount, _windowRollingUnit);

    /// <summary>Whether it is on the scorecard.</summary>
    public KpiStatus Status { get; private set; }

    /// <summary>Its position on the scorecard within its subject, lowest first.</summary>
    public int Order { get; private set; }

    /// <summary>The measurements taken, in no particular order.</summary>
    public IReadOnlyCollection<KpiMeasurement> Measurements => _measurements.AsReadOnly();

    /// <summary>
    /// The checkpoint plan, in no particular order; empty when the KPI has none. Throws when the KPI was loaded
    /// without it.
    /// </summary>
    public IReadOnlyCollection<KpiCheckpoint> Checkpoints => LoadedCheckpoints.AsReadOnly();

    /// <summary>
    /// The checkpoint plan's values, in date order; empty when the KPI has none. Throws when the KPI was loaded
    /// without it.
    /// </summary>
    public IReadOnlyList<KpiCheckpointValues> CheckpointPlan =>
        [.. LoadedCheckpoints.OrderBy(c => c.Date).Select(c => c.ToValues())];

    /// <summary>
    /// The plan, refusing a KPI loaded without it: read as empty, a plan change would add every checkpoint again
    /// and a direction change would pass at-risk values it should refuse.
    /// </summary>
    private List<KpiCheckpoint> LoadedCheckpoints => _checkpoints
        ?? throw new InvalidOperationException(
            $"KPI {Id} was loaded without its checkpoint plan. Include {nameof(Checkpoints)} in the query that loads it.");

    /// <summary>Everything the KPI is measured against, taken together.</summary>
    public KpiGoal Goal => new(Direction, Baseline, Target, AtRisk);

    /// <summary>The descriptive fields, taken together.</summary>
    public KpiDetails Details => new(Name, Description, CategoryId, Prefix, Suffix, ExpressedAs, RollingAverageOf);

    /// <summary>
    /// Changes the name, description, category, prefix, suffix, the reading shown and its rolling average. Raises
    /// nothing when nothing changed.
    /// </summary>
    public Result UpdateDetails(KpiDetailsInput input, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(input, nameof(input));

        var error = input.Validate() ?? CheckRollingAverage(input.RollingAverageOf, Window);
        if (error is not null)
            return Result.Failure(error);

        var previous = Details;
        ApplyDetails(input);
        var current = Details;

        if (current != previous)
            AddKeyedDomainEvent(() => new KpiDetailsUpdatedEvent(Id, Key, SubjectType, SubjectId, current, previous, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Changes what the KPI is measured against — its direction, baseline, target and at-risk threshold — failing when
    /// the new goal does not hold. Checked as a whole, so a baseline and target that move together never fail against
    /// each other's old value.
    /// </summary>
    /// <remarks>
    /// Raises <see cref="KpiBaselineChangedEvent"/> when the baseline changed and <see cref="KpiTargetChangedEvent"/>
    /// when the direction, target or threshold did; nothing when nothing changed.
    /// <para>
    /// Fails when a new direction leaves a checkpoint's at-risk value on the wrong side of its planned value. Reads
    /// <see cref="Checkpoints"/> on a direction change, so the KPI must be loaded with them, and throws when it was
    /// not.
    /// </para>
    /// </remarks>
    public Result ChangeGoal(KpiGoal goal, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(goal, nameof(goal));

        var error = goal.Validate();
        if (error is not null)
            return Result.Failure(error);

        var previous = Goal;
        if (goal == previous)
            return Result.Success();

        // The plan already held under the old direction, so only its at-risk values can fail under the new one.
        if (goal.Direction != previous.Direction && KpiCheckpointPlan.Validate(CheckpointPlan, goal.Direction) is not null)
            return Result.Failure(KpiErrors.DirectionConflictsWithPlan);

        ApplyGoal(goal);
        var current = Goal;

        if (current.Baseline != previous.Baseline)
            AddKeyedDomainEvent(() => new KpiBaselineChangedEvent(Id, Key, SubjectType, SubjectId, current.Baseline, previous.Baseline, actor, timestamp));

        if (current with { Baseline = previous.Baseline } != previous)
            AddKeyedDomainEvent(() => new KpiTargetChangedEvent(Id, Key, SubjectType, SubjectId, current, previous, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Changes how the KPI's measurements are windowed, failing once it has any: they were recorded under the old
    /// window, and a stored period must keep meaning what it meant. Raises nothing when nothing changed.
    /// </summary>
    /// <remarks>
    /// Reads <see cref="Measurements"/>, so the KPI must be loaded with them; loaded without, it would let a window
    /// change that should have been refused.
    /// </remarks>
    public Result ChangeMeasurementWindow(KpiMeasurementWindow window, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(window, nameof(window));

        var error = window.Validate();
        if (error is not null)
            return Result.Failure(error);

        var previous = Window;
        if (window == previous)
            return Result.Success();

        if (_measurements.Count > 0)
            return Result.Failure(KpiErrors.WindowLockedByMeasurements);

        error = CheckRollingAverage(RollingAverageOf, window);
        if (error is not null)
            return Result.Failure(error);

        ApplyWindow(window);
        var current = Window;
        AddKeyedDomainEvent(() => new KpiMeasurementWindowChangedEvent(Id, Key, SubjectType, SubjectId, current, previous, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Replaces the checkpoint plan with <paramref name="plan"/>, matching checkpoints by date: one on a day already
    /// planned is changed, one on a new day is added, and a planned day left out is removed. An empty plan clears it.
    /// Fails when the KPI is closed or the plan does not hold; raises nothing when nothing changed.
    /// </summary>
    /// <remarks>
    /// Reads <see cref="Checkpoints"/>, so the KPI must be loaded with them, and throws when it was not.
    /// </remarks>
    public Result ChangeCheckpointPlan(IReadOnlyCollection<KpiCheckpointValues> plan, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(plan, nameof(plan));
        Guard.Against.Null(actor, nameof(actor));

        if (Status == KpiStatus.Closed)
            return Result.Failure(KpiErrors.PlanLockedClosed);

        var error = KpiCheckpointPlan.Validate(plan, Direction);
        if (error is not null)
            return Result.Failure(error);

        var current = KpiCheckpointPlan.Normalize(plan);
        var previous = CheckpointPlan;
        if (current.SequenceEqual(previous))
            return Result.Success();

        var checkpoints = LoadedCheckpoints;
        var byDate = current.ToDictionary(c => c.Date);
        checkpoints.RemoveAll(c => !byDate.ContainsKey(c.Date));

        foreach (var values in current)
        {
            var existing = checkpoints.FirstOrDefault(c => c.Date == values.Date);
            if (existing is null)
                checkpoints.Add(new KpiCheckpoint(Id, values));
            else
                existing.Apply(values);
        }

        var previousByDate = previous.ToDictionary(c => c.Date);
        var added = current.Where(c => !previousByDate.ContainsKey(c.Date)).ToList();
        var removed = previous.Where(c => !byDate.ContainsKey(c.Date)).ToList();
        var revised = current
            .Where(c => previousByDate.TryGetValue(c.Date, out var before) && before != c)
            .Select(c => new KpiCheckpointRevision(previousByDate[c.Date], c))
            .ToList();
        AddKeyedDomainEvent(() => new KpiCheckpointPlanChangedEvent(Id, Key, SubjectType, SubjectId, added, removed, revised, current, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// The checkpoint a measurement whose period ends on <paramref name="periodEnd"/> is read against, or null when the
    /// KPI's own target applies.
    /// </summary>
    public KpiCheckpointValues? CheckpointFor(LocalDate periodEnd) => KpiCheckpointPlan.For(CheckpointPlan, periodEnd);

    /// <summary>Takes the KPI off the scorecard, keeping its measurements as history.</summary>
    public Result Close(EventActor actor, Instant timestamp)
    {
        if (Status == KpiStatus.Closed)
            return Result.Failure(KpiErrors.AlreadyClosed);

        Status = KpiStatus.Closed;
        AddKeyedDomainEvent(() => new KpiClosedEvent(Id, Key, SubjectType, SubjectId, actor, timestamp));

        return Result.Success();
    }

    /// <summary>Puts a closed KPI back on the scorecard.</summary>
    public Result Reopen(EventActor actor, Instant timestamp)
    {
        if (Status != KpiStatus.Closed)
            return Result.Failure(KpiErrors.NotClosed);

        Status = KpiStatus.Active;
        AddKeyedDomainEvent(() => new KpiReopenedEvent(Id, Key, SubjectType, SubjectId, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Records that the KPI is being removed with its measurements and checkpoint plan. The caller then removes it; the
    /// database removes the rest.
    /// </summary>
    /// <remarks>
    /// One event for the KPI rather than one per measurement: they go because the KPI went, and a KPI that should keep
    /// its history is closed instead.
    /// </remarks>
    public void Remove(EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(actor, nameof(actor));
        var name = Name;
        AddKeyedDomainEvent(() => new KpiRemovedEvent(Id, Key, SubjectType, SubjectId, name, actor, timestamp));
    }

    /// <summary>
    /// Puts a subject's KPIs in the given order, numbering them from zero. Each KPI whose position changed raises its
    /// own event.
    /// </summary>
    /// <param name="subjectKpis">Every KPI on one subject, open and closed.</param>
    /// <param name="orderedIds">The same KPIs' ids, each exactly once, in their new order.</param>
    public static Result Reorder(IReadOnlyCollection<Kpi> subjectKpis, IReadOnlyList<Guid> orderedIds, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(subjectKpis, nameof(subjectKpis));
        Guard.Against.Null(orderedIds, nameof(orderedIds));

        var byId = subjectKpis.ToDictionary(k => k.Id);
        if (orderedIds.Count != byId.Count || orderedIds.Distinct().Count() != orderedIds.Count || !orderedIds.All(byId.ContainsKey))
            return Result.Failure(KpiErrors.ReorderMismatch);

        if (subjectKpis.Select(k => (k.SubjectType, k.SubjectId)).Distinct().Count() > 1)
            return Result.Failure(KpiErrors.ReorderMismatch);

        for (var position = 0; position < orderedIds.Count; position++)
        {
            var kpi = byId[orderedIds[position]];
            if (kpi.Order == position)
                continue;

            var from = kpi.Order;
            kpi.Order = position;
            var to = position;
            kpi.AddKeyedDomainEvent(() => new KpiReorderedEvent(kpi.Id, kpi.Key, kpi.SubjectType, kpi.SubjectId, from, to, actor, timestamp));
        }

        return Result.Success();
    }

    /// <summary>
    /// Records a measurement, failing when the KPI is closed, the time given does not fit its window, or it clashes
    /// with another measurement: an overlapping period, the same as-of day or the same moment.
    /// </summary>
    /// <param name="input">The value and when it was taken.</param>
    /// <param name="zone">The system time zone, which a point-in-time moment's calendar day is read in.</param>
    /// <param name="actor">Who recorded it.</param>
    /// <param name="timestamp">When.</param>
    public Result<KpiMeasurement> AddMeasurement(KpiMeasurementInput input, DateTimeZone zone, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(input, nameof(input));
        input = input.AtStoredPrecision();

        var period = CheckMeasurement(input, zone, existing: null);
        if (period.IsFailure)
            return Result.Failure<KpiMeasurement>(period.Error);

        var measurement = new KpiMeasurement(Id, input, period.Value);
        _measurements.Add(measurement);
        var values = measurement.ToValues();
        AddKeyedDomainEvent(() => new KpiMeasurementAddedEvent(Id, Key, SubjectType, SubjectId, values, actor, timestamp));

        return measurement;
    }

    /// <summary>
    /// Changes a measurement, failing as <see cref="AddMeasurement"/> does. Raises nothing when nothing changed.
    /// </summary>
    /// <remarks>
    /// A point-in-time measurement whose moment is unchanged keeps the day it was stored with, so correcting its value
    /// or note after the system time zone changed does not move it to another day.
    /// </remarks>
    public Result ChangeMeasurement(Guid measurementId, KpiMeasurementInput input, DateTimeZone zone, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(input, nameof(input));
        input = input.AtStoredPrecision();

        var measurement = _measurements.FirstOrDefault(m => m.Id == measurementId);
        if (measurement is null)
            return Result.Failure(KpiErrors.MeasurementNotFound);

        var period = CheckMeasurement(input, zone, existing: measurement);
        if (period.IsFailure)
            return Result.Failure(period.Error);

        var previous = measurement.ToValues();
        measurement.Apply(input, period.Value);
        var current = measurement.ToValues();

        if (current != previous)
            AddKeyedDomainEvent(() => new KpiMeasurementChangedEvent(Id, Key, SubjectType, SubjectId, current, previous, actor, timestamp));

        return Result.Success();
    }

    /// <summary>Removes a measurement, failing when the KPI is closed.</summary>
    public Result RemoveMeasurement(Guid measurementId, EventActor actor, Instant timestamp)
    {
        if (Status == KpiStatus.Closed)
            return Result.Failure(KpiErrors.KpiClosed);

        var measurement = _measurements.FirstOrDefault(m => m.Id == measurementId);
        if (measurement is null)
            return Result.Failure(KpiErrors.MeasurementNotFound);

        _measurements.Remove(measurement);
        var values = measurement.ToValues();
        AddKeyedDomainEvent(() => new KpiMeasurementRemovedEvent(Id, Key, SubjectType, SubjectId, values, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// The latest measurement — by the end of its period, then by its moment for point-in-time readings on one day —
    /// or null when there is none. Each window keeps that order unambiguous.
    /// </summary>
    public KpiMeasurement? LatestMeasurement() =>
        _measurements.OrderByDescending(m => m.PeriodEnd).ThenByDescending(m => m.MeasuredAt).FirstOrDefault();

    /// <summary>The period the input covers under the KPI's window, or why it cannot be recorded.</summary>
    /// <param name="input">The value and when it was taken.</param>
    /// <param name="zone">The system time zone, which a new moment's calendar day is read in.</param>
    /// <param name="existing">The measurement being changed, or null when recording a new one.</param>
    private Result<KpiPeriod> CheckMeasurement(KpiMeasurementInput input, DateTimeZone zone, KpiMeasurement? existing)
    {
        if (Status == KpiStatus.Closed)
            return Result.Failure<KpiPeriod>(KpiErrors.KpiClosed);

        var error = input.Validate();
        if (error is not null)
            return Result.Failure<KpiPeriod>(error);

        var window = Window;
        if (input.PeriodRecordKind is not null && window.Kind != KpiWindowKind.PerPeriod)
            return Result.Failure<KpiPeriod>(KpiErrors.PeriodRecordOnlyPerPeriod);

        var period = window.PeriodFor(input.Time, zone);
        if (period.IsFailure)
            return period;

        if (window.Kind == KpiWindowKind.PointInTime && existing is not null && existing.MeasuredAt == input.Time.At)
            period = existing.Period;

        var others = _measurements.Where(m => m.Id != existing?.Id);
        var clash = window.Kind switch
        {
            KpiWindowKind.PerPeriod => others.Any(m => m.Period.Overlaps(period.Value)) ? KpiErrors.PeriodOverlaps : null,
            KpiWindowKind.PointInTime => others.Any(m => m.MeasuredAt == input.Time.At) ? KpiErrors.MomentTaken : null,
            _ => others.Any(m => m.PeriodEnd == period.Value.End) ? KpiErrors.AsOfTaken : null,
        };

        return clash is null ? period : Result.Failure<KpiPeriod>(clash);
    }

    /// <summary>Why a rolling average cannot be read on a KPI with this window, or null when it can.</summary>
    private static string? CheckRollingAverage(int? rollingAverageOf, KpiMeasurementWindow window) =>
        rollingAverageOf is not null && window.Kind != KpiWindowKind.PerPeriod
            ? KpiErrors.RollingAverageOnlyPerPeriod
            : null;

    private void ApplyWindow(KpiMeasurementWindow window)
    {
        _windowKind = window.Kind;
        _windowAnchorDate = window.AnchorDate;
        _windowRollingCount = window.RollingCount;
        _windowRollingUnit = window.RollingUnit;
    }

    private void ApplyDetails(KpiDetailsInput input)
    {
        Name = input.Name;
        Description = input.Description;
        CategoryId = input.CategoryId;
        Prefix = input.Prefix;
        Suffix = input.Suffix;
        ExpressedAs = input.ExpressedAs;
        RollingAverageOf = input.RollingAverageOf;
    }

    private void ApplyGoal(KpiGoal goal)
    {
        Direction = goal.Direction;
        _baselineValue = goal.Baseline.Value;
        _baselineWindowStart = goal.Baseline.Window?.Start;
        _baselineWindowEnd = goal.Baseline.Window?.End;
        _targetKind = goal.Target.Kind;
        _targetValue = goal.Target.Value;
        _atRiskKind = goal.AtRisk?.Kind;
        _atRiskValue = goal.AtRisk?.Value;
    }

    /// <summary>
    /// Raises an event that carries <see cref="Key"/>, waiting for the first save to assign it.
    /// </summary>
    /// <remarks>
    /// Every event goes through here, not only the added one: the import creates a KPI and records its measurements,
    /// or closes it, before the first save, and those events must carry the key and follow the added event.
    /// <para>
    /// The factory may run after later changes, so anything mutable it carries must be captured in locals by the
    /// caller. Only the members that never change — Id, Key, SubjectType and SubjectId — may be read inside it.
    /// </para>
    /// </remarks>
    private void AddKeyedDomainEvent(Func<DomainEvent> build)
    {
        if (Key == 0)
            AddPostPersistenceAction(() => AddDomainEvent(build()));
        else
            AddDomainEvent(build());
    }
}

/// <summary>
/// What a caller supplies for a KPI's descriptive fields.
/// </summary>
/// <param name="Name">What the KPI is called.</param>
/// <param name="Description">What it measures and why.</param>
/// <param name="CategoryId">The category it is grouped under, or null.</param>
/// <param name="Prefix">A symbol shown before a value.</param>
/// <param name="Suffix">A unit shown after a value.</param>
/// <param name="ExpressedAs">Which reading the scorecard shows.</param>
/// <param name="RollingAverageOf">How many of the latest measurements the reading averages, or null for the latest alone.</param>
public record KpiDetailsInput(
    string Name,
    string? Description,
    Guid? CategoryId,
    string? Prefix,
    string? Suffix,
    KpiExpressedAs ExpressedAs,
    int? RollingAverageOf = null)
{
    /// <summary>Why these details cannot be held, or null when they can.</summary>
    internal string? Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
            return "A KPI needs a name.";
        if (Name.Trim().Length > Kpi.NameMaxLength)
            return $"A KPI name can be at most {Kpi.NameMaxLength} characters.";
        if (Description?.Trim().Length > Kpi.DescriptionMaxLength)
            return $"A KPI description can be at most {Kpi.DescriptionMaxLength} characters.";
        if (Prefix?.Trim().Length > Kpi.AffixMaxLength || Suffix?.Trim().Length > Kpi.AffixMaxLength)
            return $"A prefix or suffix can be at most {Kpi.AffixMaxLength} characters.";
        if (CategoryId == Guid.Empty)
            return "A category id cannot be empty.";
        if (!Enum.IsDefined(ExpressedAs))
            return $"Unsupported reading: {ExpressedAs}.";
        if (RollingAverageOf is < Kpi.MinRollingAverageOf or > Kpi.MaxRollingAverageOf)
            return KpiErrors.RollingAverageOutOfRange;

        return null;
    }
}

/// <summary>
/// What a caller supplies to add a KPI: its descriptive fields, what it is measured against and how its measurements
/// are windowed.
/// </summary>
/// <param name="Window">How its measurements are windowed; per period when not given.</param>
public sealed record KpiDefinition(
    string Name,
    string? Description,
    Guid? CategoryId,
    string? Prefix,
    string? Suffix,
    KpiExpressedAs ExpressedAs,
    KpiGoal Goal,
    KpiMeasurementWindow? Window = null,
    int? RollingAverageOf = null)
    : KpiDetailsInput(Name, Description, CategoryId, Prefix, Suffix, ExpressedAs, RollingAverageOf)
{
    /// <summary>The window given, or per period.</summary>
    internal KpiMeasurementWindow WindowOrDefault => Window ?? KpiMeasurementWindow.PerPeriod;

    /// <summary>Why this definition cannot be held, or null when it can.</summary>
    internal new string? Validate() =>
        base.Validate()
        ?? (Goal is null ? "A KPI needs a goal." : Goal.Validate())
        ?? WindowOrDefault.Validate();
}
