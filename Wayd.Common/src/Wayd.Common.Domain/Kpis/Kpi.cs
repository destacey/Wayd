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

    private readonly List<KpiMeasurement> _measurements = [];

    private string _name = default!;
    private string? _description;
    private string? _prefix;
    private string? _suffix;

    private Kpi() { }

    private Kpi(string subjectType, Guid subjectId, KpiDefinition definition, int order)
    {
        SubjectType = subjectType;
        SubjectId = subjectId;
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

        var error = definition.Validate();
        if (error is not null)
            return Result.Failure<Kpi>(error);

        // The registered key rather than the caller's spelling, so a KPI is stored under one casing.
        var kpi = new Kpi(descriptor.Value.Key, subjectId, definition, order);

        var id = kpi.Id;
        var details = kpi.Details;
        var goal = kpi.Goal;
        var source = kpi.Source;
        var type = kpi.SubjectType;
        kpi.AddKeyedDomainEvent(() => new KpiAddedEvent(id, kpi.Key, type, subjectId, details, goal, source, order, actor, timestamp));

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
    public KpiBaseline Baseline { get; private set; } = KpiBaseline.None;

    /// <summary>What it aims for, as entered.</summary>
    public KpiTarget Target { get; private set; } = default!;

    /// <summary>The optional line short of the target past which a measurement is at risk.</summary>
    public KpiAtRiskThreshold? AtRisk { get; private set; }

    /// <summary>Where its measurements come from.</summary>
    public KpiSource Source { get; private set; }

    /// <summary>Which reading of the raw value the scorecard shows.</summary>
    public KpiExpressedAs ExpressedAs { get; private set; }

    /// <summary>Whether it is on the scorecard.</summary>
    public KpiStatus Status { get; private set; }

    /// <summary>Its position on the scorecard within its subject, lowest first.</summary>
    public int Order { get; private set; }

    /// <summary>The measurements taken, in no particular order.</summary>
    public IReadOnlyCollection<KpiMeasurement> Measurements => _measurements.AsReadOnly();

    /// <summary>Everything the KPI is measured against, taken together.</summary>
    public KpiGoal Goal => new(Direction, Baseline, Target, AtRisk);

    /// <summary>The descriptive fields, taken together.</summary>
    public KpiDetails Details => new(Name, Description, CategoryId, Prefix, Suffix, ExpressedAs);

    /// <summary>
    /// Changes the name, description, category, prefix, suffix and the reading shown. Raises nothing when nothing
    /// changed.
    /// </summary>
    public Result UpdateDetails(KpiDetailsInput input, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(input, nameof(input));

        var error = input.Validate();
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

        ApplyGoal(goal);
        var current = Goal;

        if (current.Baseline != previous.Baseline)
            AddKeyedDomainEvent(() => new KpiBaselineChangedEvent(Id, Key, SubjectType, SubjectId, current.Baseline, previous.Baseline, actor, timestamp));

        if (current with { Baseline = previous.Baseline } != previous)
            AddKeyedDomainEvent(() => new KpiTargetChangedEvent(Id, Key, SubjectType, SubjectId, current, previous, actor, timestamp));

        return Result.Success();
    }

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
    /// Records that the KPI is being removed with its measurements. The caller then removes it; the database removes
    /// its measurements.
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
    /// Records a measurement, failing when the KPI is closed or the period overlaps another measurement's.
    /// </summary>
    public Result<KpiMeasurement> AddMeasurement(KpiMeasurementInput input, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(input, nameof(input));

        var error = CheckMeasurement(input, exceptId: null);
        if (error is not null)
            return Result.Failure<KpiMeasurement>(error);

        var measurement = new KpiMeasurement(Id, input);
        _measurements.Add(measurement);
        var values = measurement.ToValues();
        AddKeyedDomainEvent(() => new KpiMeasurementAddedEvent(Id, Key, SubjectType, SubjectId, values, actor, timestamp));

        return measurement;
    }

    /// <summary>
    /// Changes a measurement, failing when the KPI is closed or the new period overlaps another measurement's. Raises
    /// nothing when nothing changed.
    /// </summary>
    public Result ChangeMeasurement(Guid measurementId, KpiMeasurementInput input, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(input, nameof(input));

        var measurement = _measurements.FirstOrDefault(m => m.Id == measurementId);
        if (measurement is null)
            return Result.Failure(KpiErrors.MeasurementNotFound);

        var error = CheckMeasurement(input, exceptId: measurementId);
        if (error is not null)
            return Result.Failure(error);

        var previous = measurement.ToValues();
        measurement.Apply(input);
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
    /// The latest measurement, by the end of its period, or null when there is none. Periods never overlap, so the
    /// order is unambiguous.
    /// </summary>
    public KpiMeasurement? LatestMeasurement() => _measurements.MaxBy(m => m.Period.End);

    private string? CheckMeasurement(KpiMeasurementInput input, Guid? exceptId)
    {
        if (Status == KpiStatus.Closed)
            return KpiErrors.KpiClosed;

        var error = input.Validate();
        if (error is not null)
            return error;

        return _measurements.Any(m => m.Id != exceptId && m.Period.Overlaps(input.Period))
            ? KpiErrors.PeriodOverlaps
            : null;
    }

    private void ApplyDetails(KpiDetailsInput input)
    {
        Name = input.Name;
        Description = input.Description;
        CategoryId = input.CategoryId;
        Prefix = input.Prefix;
        Suffix = input.Suffix;
        ExpressedAs = input.ExpressedAs;
    }

    private void ApplyGoal(KpiGoal goal)
    {
        Direction = goal.Direction;
        Baseline = goal.Baseline;
        Target = goal.Target;
        AtRisk = goal.AtRisk;
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
public record KpiDetailsInput(string Name, string? Description, Guid? CategoryId, string? Prefix, string? Suffix, KpiExpressedAs ExpressedAs)
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

        return null;
    }
}

/// <summary>
/// What a caller supplies to add a KPI: its descriptive fields and what it is measured against.
/// </summary>
public sealed record KpiDefinition(
    string Name,
    string? Description,
    Guid? CategoryId,
    string? Prefix,
    string? Suffix,
    KpiExpressedAs ExpressedAs,
    KpiGoal Goal)
    : KpiDetailsInput(Name, Description, CategoryId, Prefix, Suffix, ExpressedAs)
{
    /// <summary>Why this definition cannot be held, or null when it can.</summary>
    internal new string? Validate() => base.Validate() ?? (Goal is null ? "A KPI needs a goal." : Goal.Validate());
}
