using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Kpis;
using Wayd.Common.Domain.Kpis;
using Wayd.Common.Domain.Tests.Data;

namespace Wayd.Common.Domain.Tests.Sut.Kpis;

public sealed class KpiTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 16, 9, 30);
    private static readonly EventActor Actor = EventActor.User("user-1", Guid.NewGuid());
    private static readonly LocalDate Jan1 = new(2026, 1, 1);

    // Six hours behind UTC in winter, so a moment early in the UTC day falls on the day before.
    private static readonly DateTimeZone Zone = DateTimeZoneProviders.Tzdb["America/Chicago"];

    private readonly KpiFaker _faker = new();

    private static KpiDefinition Definition(KpiGoal? goal = null, KpiMeasurementWindow? window = null, int? rollingAverageOf = null) => new(
        " Cycle time ",
        "Days from start to done.",
        null,
        null,
        " days ",
        KpiExpressedAs.Value,
        goal ?? new KpiGoal(KpiTargetDirection.Decrease, new KpiBaseline(20, null), KpiTarget.RelativeChange(25), null),
        window,
        rollingAverageOf);

    private static KpiMeasurementInput Measurement(double value, LocalDate start, LocalDate end) =>
        Taken(value, KpiMeasurementTime.ForPeriod(new KpiPeriod(start, end)), end);

    private static KpiMeasurementInput Taken(double value, KpiMeasurementTime time, LocalDate measuredOn) =>
        new(value, time, null, null, measuredOn, Guid.NewGuid(), KpiMeasurementSource.Manual, null, null);

    private static Kpi WithWindow(KpiMeasurementWindow window) =>
        Kpi.Create(KpiFaker.TestSubject.Key, Guid.NewGuid(), Definition(window: window), 0, Actor, Now).Value;

    [Fact]
    public void Create_ShouldAddAnActiveManualKpiUnderTheRegisteredKey_AndRaiseAddedOnceKeyed()
    {
        // Arrange
        var subjectId = Guid.NewGuid();

        // Act
        var result = Kpi.Create(" TEST.Kpis.Subject ", subjectId, Definition(), 3, Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var kpi = result.Value;
        kpi.SubjectType.Should().Be(KpiFaker.TestSubject.Key);
        kpi.SubjectId.Should().Be(subjectId);
        kpi.Name.Should().Be("Cycle time");
        kpi.Suffix.Should().Be("days");
        kpi.Status.Should().Be(KpiStatus.Active);
        kpi.Source.Should().Be(KpiSource.Manual);
        kpi.Order.Should().Be(3);
        kpi.Goal.TargetValue.Should().Be(15);

        // The key is assigned on the first save, so the event waits for it.
        kpi.DomainEvents.Should().BeEmpty();
        kpi.ExecutePostPersistenceActions();
        var added = kpi.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<KpiAddedEvent>().Subject;
        added.Id.Should().Be(kpi.Id);
        added.SubjectId.Should().Be(subjectId);
        added.Details.Name.Should().Be("Cycle time");
        added.Goal.Should().Be(kpi.Goal);
        added.Order.Should().Be(3);
        added.RelatedAggregates.Should().Equal(new AggregateReference("Gadget", subjectId));
    }

    [Fact]
    public void Create_ShouldHoldEveryLaterEventUntilTheKeyIsAssigned_AndRaiseThemAfterAdded()
    {
        // Arrange
        var kpi = Kpi.Create(KpiFaker.TestSubject.Key, Guid.NewGuid(), Definition(), 0, Actor, Now).Value;

        // Act
        kpi.AddMeasurement(Measurement(14, Jan1, new LocalDate(2026, 1, 31)), Zone, Actor, Now);
        kpi.Close(Actor, Now);

        // Assert
        kpi.DomainEvents.Should().BeEmpty();
        kpi.ExecutePostPersistenceActions();
        kpi.DomainEvents.Select(e => e.GetType()).Should()
            .Equal(typeof(KpiAddedEvent), typeof(KpiMeasurementAddedEvent), typeof(KpiClosedEvent));
    }

    [Fact]
    public void Create_ShouldFail_ForAnUnregisteredSubjectType()
    {
        // Arrange
        const string subjectType = "test.kpis.nothing";

        // Act
        var result = Kpi.Create(subjectType, Guid.NewGuid(), Definition(), 0, Actor, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(KpiErrors.UnknownSubjectType(subjectType));
    }

    [Fact]
    public void Create_ShouldFail_WhenTheGoalCannotHold()
    {
        // Arrange
        var goal = new KpiGoal(KpiTargetDirection.Increase, KpiBaseline.None, KpiTarget.RelativeChange(10), null);

        // Act
        var result = Kpi.Create(KpiFaker.TestSubject.Key, Guid.NewGuid(), Definition(goal), 0, Actor, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(KpiErrors.BaselineRequired);
    }

    [Fact]
    public void UpdateDetails_ShouldRaiseBothEnds_WhenSomethingChanged()
    {
        // Arrange
        var kpi = _faker.Generate();
        var previous = kpi.Details;
        var categoryId = Guid.NewGuid();

        // Act
        var result = kpi.UpdateDetails(new KpiDetailsInput("Lead time", null, categoryId, null, "days", KpiExpressedAs.ProgressToTarget), Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        kpi.CategoryId.Should().Be(categoryId);
        kpi.ExpressedAs.Should().Be(KpiExpressedAs.ProgressToTarget);
        var updated = kpi.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<KpiDetailsUpdatedEvent>().Subject;
        updated.Previous.Should().Be(previous);
        updated.Details.Should().Be(new KpiDetails("Lead time", null, categoryId, null, "days", KpiExpressedAs.ProgressToTarget));
    }

    [Fact]
    public void UpdateDetails_ShouldRaiseNothing_WhenOnlyWhitespaceDiffers()
    {
        // Arrange
        var kpi = _faker.Generate();
        var input = new KpiDetailsInput($"  {kpi.Name}  ", " ", kpi.CategoryId, kpi.Prefix, kpi.Suffix, kpi.ExpressedAs);

        // Act
        var result = kpi.UpdateDetails(input, Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        kpi.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void UpdateDetails_ShouldFail_WithoutAName()
    {
        // Arrange
        var kpi = _faker.Generate();
        var name = kpi.Name;

        // Act
        var result = kpi.UpdateDetails(new KpiDetailsInput(" ", null, null, null, null, KpiExpressedAs.Value), Actor, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        kpi.Name.Should().Be(name);
        kpi.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeGoal_ShouldRaiseTargetChangedWithBothEnds_WhenOnlyTheTargetMoved()
    {
        // Arrange
        var kpi = _faker.Generate();
        var previous = kpi.Goal;
        var goal = previous with { Target = KpiTarget.RelativeChange(50), AtRisk = KpiAtRiskThreshold.ProgressToTarget(80) };

        // Act
        var result = kpi.ChangeGoal(goal, Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        kpi.Goal.TargetValue.Should().Be(15);
        kpi.Goal.AtRiskValue.Should().Be(14);
        var changed = kpi.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<KpiTargetChangedEvent>().Subject;
        changed.Previous.Should().Be(previous);
        changed.Goal.Should().Be(kpi.Goal);
    }

    [Fact]
    public void ChangeGoal_ShouldRaiseOnlyBaselineChanged_AndMoveARelativeTarget_WhenOnlyTheBaselineMoved()
    {
        // Arrange
        var kpi = _faker.WithTarget(KpiTarget.RelativeChange(50)).Generate();
        var window = new KpiPeriod(Jan1, new LocalDate(2026, 3, 31));

        // Act
        var result = kpi.ChangeGoal(kpi.Goal with { Baseline = new KpiBaseline(12, window) }, Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        kpi.Goal.TargetValue.Should().Be(18);
        var changed = kpi.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<KpiBaselineChangedEvent>().Subject;
        changed.Previous.Should().Be(new KpiBaseline(10, null));
        changed.Baseline.Should().Be(new KpiBaseline(12, window));
    }

    [Fact]
    public void ChangeGoal_ShouldAcceptABaselineAndTargetThatMoveTogether_AndRaiseBoth()
    {
        // Arrange
        var kpi = _faker.Generate();
        var goal = kpi.Goal with { Baseline = new KpiBaseline(40, null), Target = KpiTarget.Absolute(50) };

        // Act
        var result = kpi.ChangeGoal(goal, Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        kpi.Baseline.Value.Should().Be(40);
        kpi.Target.Value.Should().Be(50);
        kpi.DomainEvents.Select(e => e.GetType()).Should().Equal(typeof(KpiBaselineChangedEvent), typeof(KpiTargetChangedEvent));
    }

    [Fact]
    public void ChangeGoal_ShouldRaiseNothing_WhenTheGoalIsTheSame()
    {
        // Arrange
        var kpi = _faker.Generate();

        // Act
        var result = kpi.ChangeGoal(kpi.Goal with { Target = KpiTarget.Absolute(20) }, Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        kpi.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeGoal_ShouldFail_AndChangeNothing_WhenTheBaselineAlreadyMeetsTheTarget()
    {
        // Arrange
        var kpi = _faker.Generate();
        var previous = kpi.Goal;

        // Act
        var result = kpi.ChangeGoal(previous with { Direction = KpiTargetDirection.Decrease }, Actor, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(KpiErrors.BaselineMeetsTargetDecrease);
        kpi.Goal.Should().Be(previous);
        kpi.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeGoal_ShouldFail_WhenClearingTheBaselineOfARelativeTarget()
    {
        // Arrange
        var kpi = _faker.WithTarget(KpiTarget.RelativeChange(50)).Generate();

        // Act
        var result = kpi.ChangeGoal(kpi.Goal with { Baseline = KpiBaseline.None }, Actor, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(KpiErrors.BaselineRequired);
        kpi.Baseline.Value.Should().Be(10);
        kpi.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Close_ShouldTakeItOffTheScorecard_AndReopenShouldPutItBack()
    {
        // Arrange
        var kpi = _faker.Generate();

        // Act
        var closed = kpi.Close(Actor, Now);
        var closedAgain = kpi.Close(Actor, Now);
        var reopened = kpi.Reopen(Actor, Now);
        var reopenedAgain = kpi.Reopen(Actor, Now);

        // Assert
        closed.IsSuccess.Should().BeTrue();
        closedAgain.Error.Should().Be(KpiErrors.AlreadyClosed);
        reopened.IsSuccess.Should().BeTrue();
        reopenedAgain.Error.Should().Be(KpiErrors.NotClosed);
        kpi.Status.Should().Be(KpiStatus.Active);
        kpi.DomainEvents.Select(e => e.GetType()).Should().Equal(typeof(KpiClosedEvent), typeof(KpiReopenedEvent));
    }

    [Fact]
    public void Remove_ShouldRaiseRemovedWithTheName()
    {
        // Arrange
        var kpi = _faker.Generate();

        // Act
        kpi.Remove(Actor, Now);

        // Assert
        var removed = kpi.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<KpiRemovedEvent>().Subject;
        removed.Name.Should().Be(kpi.Name);
        removed.SubjectId.Should().Be(kpi.SubjectId);
    }

    [Fact]
    public void Reorder_ShouldNumberFromZero_AndRaiseOnlyForTheKpisThatMoved()
    {
        // Arrange
        var subjectId = Guid.NewGuid();
        var first = _faker.WithSubjectId(subjectId).WithOrder(0).Generate();
        var second = _faker.WithSubjectId(subjectId).WithOrder(1).Generate();
        var third = _faker.WithSubjectId(subjectId).WithOrder(2).Generate();

        // Act
        var result = Kpi.Reorder([first, second, third], [third.Id, second.Id, first.Id], Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        (first.Order, second.Order, third.Order).Should().Be((2, 1, 0));
        second.DomainEvents.Should().BeEmpty();
        var moved = third.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<KpiReorderedEvent>().Subject;
        (moved.FromOrder, moved.ToOrder).Should().Be((2, 0));
        first.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<KpiReorderedEvent>();
    }

    [Fact]
    public void Reorder_ShouldFail_WhenTheOrderOmitsAKpi()
    {
        // Arrange
        var subjectId = Guid.NewGuid();
        var first = _faker.WithSubjectId(subjectId).WithOrder(0).Generate();
        var second = _faker.WithSubjectId(subjectId).WithOrder(1).Generate();

        // Act
        var result = Kpi.Reorder([first, second], [second.Id, second.Id], Actor, Now);

        // Assert
        result.Error.Should().Be(KpiErrors.ReorderMismatch);
        (first.Order, second.Order).Should().Be((0, 1));
    }

    [Fact]
    public void Reorder_ShouldFail_ForKpisOnDifferentSubjects()
    {
        // Arrange
        var first = _faker.WithSubjectId(Guid.NewGuid()).WithOrder(0).Generate();
        var second = _faker.WithSubjectId(Guid.NewGuid()).WithOrder(1).Generate();

        // Act
        var result = Kpi.Reorder([first, second], [second.Id, first.Id], Actor, Now);

        // Assert
        result.Error.Should().Be(KpiErrors.ReorderMismatch);
        first.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AddMeasurement_ShouldRecordTheRawValue_AndRaiseAdded()
    {
        // Arrange
        var kpi = _faker.Generate();
        var input = Measurement(14, Jan1, new LocalDate(2026, 1, 31)) with { SampleSize = 12, Note = " January " };

        // Act
        var result = kpi.AddMeasurement(input, Zone, Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        kpi.Measurements.Should().ContainSingle().Which.Should().BeSameAs(result.Value);
        result.Value.Note.Should().Be("January");
        var added = kpi.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<KpiMeasurementAddedEvent>().Subject;
        added.Measurement.Should().Be(new KpiMeasurementValues(
            result.Value.Id, 14, input.Time.Period!, null, null, input.MeasuredOn, input.MeasuredById, KpiMeasurementSource.Manual, 12, "January"));
    }

    [Theory]
    [InlineData(2026, 1, 31, 2026, 2, 28)]
    [InlineData(2025, 12, 1, 2026, 1, 1)]
    [InlineData(2026, 1, 10, 2026, 1, 20)]
    public void AddMeasurement_ShouldFail_WhenThePeriodOverlapsAnother(int sy, int sm, int sd, int ey, int em, int ed)
    {
        // Arrange
        var kpi = _faker.Generate();
        kpi.AddMeasurement(Measurement(14, Jan1, new LocalDate(2026, 1, 31)), Zone, Actor, Now);
        kpi.ClearDomainEvents();

        // Act
        var result = kpi.AddMeasurement(Measurement(15, new LocalDate(sy, sm, sd), new LocalDate(ey, em, ed)), Zone, Actor, Now);

        // Assert
        result.Error.Should().Be(KpiErrors.PeriodOverlaps);
        kpi.Measurements.Should().ContainSingle();
        kpi.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AddMeasurement_ShouldAcceptTheNextDay()
    {
        // Arrange
        var kpi = _faker.Generate();
        kpi.AddMeasurement(Measurement(14, Jan1, new LocalDate(2026, 1, 31)), Zone, Actor, Now);

        // Act
        var result = kpi.AddMeasurement(Measurement(15, new LocalDate(2026, 2, 1), new LocalDate(2026, 2, 28)), Zone, Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        kpi.LatestMeasurement()!.Value.Should().Be(15);
    }

    [Fact]
    public void AddMeasurement_ShouldFail_OnAClosedKpi()
    {
        // Arrange
        var kpi = _faker.WithStatus(KpiStatus.Closed).Generate();

        // Act
        var result = kpi.AddMeasurement(Measurement(14, Jan1, Jan1), Zone, Actor, Now);

        // Assert
        result.Error.Should().Be(KpiErrors.KpiClosed);
        kpi.Measurements.Should().BeEmpty();
    }

    [Fact]
    public void AddMeasurement_ShouldFail_WhenThePeriodRecordHasAKindButNoId()
    {
        // Arrange
        var kpi = _faker.Generate();
        var input = Measurement(14, Jan1, Jan1) with { PeriodRecordKind = KpiPeriodRecordKind.Sprint };

        // Act
        var result = kpi.AddMeasurement(input, Zone, Actor, Now);

        // Assert
        result.Error.Should().Be(KpiErrors.PeriodRecordIncomplete);
    }

    [Fact]
    public void ChangeMeasurement_ShouldRaiseBothEnds_AndMayKeepItsOwnPeriod()
    {
        // Arrange
        var kpi = _faker.Generate();
        var measurement = kpi.AddMeasurement(Measurement(14, Jan1, new LocalDate(2026, 1, 31)), Zone, Actor, Now).Value;
        var previous = new KpiMeasurementValues(measurement.Id, measurement.Value, measurement.Period, null, null,
            measurement.MeasuredOn, measurement.MeasuredById, measurement.Source, null, null);
        kpi.ClearDomainEvents();
        var input = Measurement(13, Jan1, new LocalDate(2026, 1, 31)) with { MeasuredById = measurement.MeasuredById };

        // Act
        var result = kpi.ChangeMeasurement(measurement.Id, input, Zone, Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        measurement.Value.Should().Be(13);
        var changed = kpi.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<KpiMeasurementChangedEvent>().Subject;
        changed.Previous.Should().Be(previous);
        changed.Measurement.Value.Should().Be(13);
    }

    [Fact]
    public void ChangeMeasurement_ShouldFail_WhenTheNewPeriodOverlapsAnother()
    {
        // Arrange
        var kpi = _faker.Generate();
        var january = kpi.AddMeasurement(Measurement(14, Jan1, new LocalDate(2026, 1, 31)), Zone, Actor, Now).Value;
        kpi.AddMeasurement(Measurement(15, new LocalDate(2026, 2, 1), new LocalDate(2026, 2, 28)), Zone, Actor, Now);
        kpi.ClearDomainEvents();

        // Act
        var result = kpi.ChangeMeasurement(january.Id, Measurement(14, Jan1, new LocalDate(2026, 2, 1)), Zone, Actor, Now);

        // Assert
        result.Error.Should().Be(KpiErrors.PeriodOverlaps);
        january.Period.End.Should().Be(new LocalDate(2026, 1, 31));
        kpi.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void RemoveMeasurement_ShouldRaiseTheValuesRemoved()
    {
        // Arrange
        var kpi = _faker.Generate();
        var measurement = kpi.AddMeasurement(Measurement(14, Jan1, Jan1), Zone, Actor, Now).Value;
        kpi.ClearDomainEvents();

        // Act
        var result = kpi.RemoveMeasurement(measurement.Id, Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        kpi.Measurements.Should().BeEmpty();
        kpi.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<KpiMeasurementRemovedEvent>()
            .Which.Measurement.MeasurementId.Should().Be(measurement.Id);
    }

    [Fact]
    public void RemoveMeasurement_ShouldFail_ForAnUnknownMeasurement()
    {
        // Arrange
        var kpi = _faker.Generate();

        // Act
        var result = kpi.RemoveMeasurement(Guid.NewGuid(), Actor, Now);

        // Assert
        result.Error.Should().Be(KpiErrors.MeasurementNotFound);
    }

    [Fact]
    public void LatestMeasurement_ShouldBeTheOneWhosePeriodEndsLast_WhateverTheOrderAdded()
    {
        // Arrange
        var kpi = _faker.Generate();
        kpi.AddMeasurement(Measurement(15, new LocalDate(2026, 3, 1), new LocalDate(2026, 3, 31)), Zone, Actor, Now);
        kpi.AddMeasurement(Measurement(14, Jan1, new LocalDate(2026, 1, 31)), Zone, Actor, Now);

        // Act
        var latest = kpi.LatestMeasurement();

        // Assert
        latest!.Value.Should().Be(15);
    }

    [Fact]
    public void Create_ShouldDefaultToPerPeriod_AndCarryTheWindowOnAdded()
    {
        // Arrange
        var window = KpiMeasurementWindow.Cumulative(new LocalDate(2026, 2, 16));

        // Act
        var perPeriod = Kpi.Create(KpiFaker.TestSubject.Key, Guid.NewGuid(), Definition(), 0, Actor, Now).Value;
        var cumulative = Kpi.Create(KpiFaker.TestSubject.Key, Guid.NewGuid(), Definition(window: window), 0, Actor, Now).Value;

        // Assert
        perPeriod.Window.Should().Be(KpiMeasurementWindow.PerPeriod);
        cumulative.Window.Should().Be(window);
        cumulative.ExecutePostPersistenceActions();
        cumulative.DomainEvents.OfType<KpiAddedEvent>().Single().Window.Should().Be(window);
    }

    [Theory]
    [MemberData(nameof(InvalidWindows))]
    public void Create_ShouldFail_ForAWindowThatDoesNotHoldTogether(KpiMeasurementWindow window, string error)
    {
        // Act
        var result = Kpi.Create(KpiFaker.TestSubject.Key, Guid.NewGuid(), Definition(window: window), 0, Actor, Now);

        // Assert
        result.Error.Should().Be(error);
    }

    public static TheoryData<KpiMeasurementWindow, string> InvalidWindows() => new()
    {
        { new KpiMeasurementWindow(KpiWindowKind.Cumulative, null, null, null), KpiErrors.CumulativeAnchorRequired },
        { new KpiMeasurementWindow(KpiWindowKind.PerPeriod, Jan1, null, null), KpiErrors.AnchorOnlyForCumulative },
        { new KpiMeasurementWindow(KpiWindowKind.Rolling, null, 90, null), KpiErrors.RollingLengthRequired },
        { KpiMeasurementWindow.Rolling(0, KpiRollingUnit.Days), KpiErrors.RollingCountOutOfRange },
        { KpiMeasurementWindow.Rolling(KpiMeasurementWindow.MaxRollingDays + 1, KpiRollingUnit.Days), KpiErrors.RollingCountOutOfRange },
        { new KpiMeasurementWindow(KpiWindowKind.PointInTime, null, 7, KpiRollingUnit.Days), KpiErrors.RollingLengthOnlyForRolling },
    };

    [Fact]
    public void Create_ShouldFail_ForARollingAverageOnAWindowThatIsNotPerPeriod()
    {
        // Act
        var result = Kpi.Create(KpiFaker.TestSubject.Key, Guid.NewGuid(),
            Definition(window: KpiMeasurementWindow.Rolling(90, KpiRollingUnit.Days), rollingAverageOf: 7), 0, Actor, Now);

        // Assert
        result.Error.Should().Be(KpiErrors.RollingAverageOnlyPerPeriod);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(Kpi.MaxRollingAverageOf + 1)]
    public void Create_ShouldFail_ForARollingAverageOutOfRange(int count)
    {
        // Act
        var result = Kpi.Create(KpiFaker.TestSubject.Key, Guid.NewGuid(), Definition(rollingAverageOf: count), 0, Actor, Now);

        // Assert
        result.Error.Should().Be(KpiErrors.RollingAverageOutOfRange);
    }

    [Fact]
    public void AddMeasurement_ShouldCoverTheAnchorToTheAsOfDay_OnACumulativeKpi()
    {
        // Arrange
        var anchor = new LocalDate(2026, 2, 16);
        var kpi = WithWindow(KpiMeasurementWindow.Cumulative(anchor));
        var friday = new LocalDate(2026, 2, 20);

        // Act
        var first = kpi.AddMeasurement(Taken(9, KpiMeasurementTime.AsOfDate(friday), friday), Zone, Actor, Now);
        var second = kpi.AddMeasurement(Taken(8, KpiMeasurementTime.AsOfDate(friday.PlusWeeks(1)), friday.PlusWeeks(1)), Zone, Actor, Now);

        // Assert
        first.IsSuccess.Should().BeTrue(first.IsFailure ? first.Error : null);
        second.IsSuccess.Should().BeTrue(second.IsFailure ? second.Error : null);
        first.Value.Period.Should().Be(new KpiPeriod(anchor, friday));
        second.Value.Period.Should().Be(new KpiPeriod(anchor, friday.PlusWeeks(1)));
        kpi.LatestMeasurement()!.Value.Should().Be(8);
    }

    [Fact]
    public void AddMeasurement_ShouldFail_BeforeACumulativeKpisAnchor()
    {
        // Arrange
        var anchor = new LocalDate(2026, 2, 16);
        var kpi = WithWindow(KpiMeasurementWindow.Cumulative(anchor));

        // Act
        var result = kpi.AddMeasurement(Taken(9, KpiMeasurementTime.AsOfDate(anchor.PlusDays(-1)), anchor), Zone, Actor, Now);

        // Assert
        result.Error.Should().Be(KpiErrors.AsOfBeforeAnchor);
    }

    [Fact]
    public void AddMeasurement_ShouldCoverTheRollingDaysEndingOnTheAsOfDay()
    {
        // Arrange
        var kpi = WithWindow(KpiMeasurementWindow.Rolling(90, KpiRollingUnit.Days));
        var asOf = new LocalDate(2026, 3, 31);

        // Act
        var result = kpi.AddMeasurement(Taken(6, KpiMeasurementTime.AsOfDate(asOf), asOf), Zone, Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Period.Should().Be(new KpiPeriod(asOf.PlusDays(-89), asOf));
    }

    [Fact]
    public void AddMeasurement_ShouldFail_ForASecondValueAsOfTheSameDay()
    {
        // Arrange
        var kpi = WithWindow(KpiMeasurementWindow.Rolling(90, KpiRollingUnit.Days));
        var asOf = new LocalDate(2026, 3, 31);
        kpi.AddMeasurement(Taken(6, KpiMeasurementTime.AsOfDate(asOf), asOf), Zone, Actor, Now);

        // Act
        var result = kpi.AddMeasurement(Taken(7, KpiMeasurementTime.AsOfDate(asOf), asOf), Zone, Actor, Now);

        // Assert
        result.Error.Should().Be(KpiErrors.AsOfTaken);
        kpi.Measurements.Should().ContainSingle();
    }

    [Fact]
    public void AddMeasurement_ShouldTakeSeveralPointInTimeReadingsADay_OnTheirDayInTheZone()
    {
        // Arrange
        var kpi = WithWindow(KpiMeasurementWindow.PointInTime);
        var morning = Instant.FromUtc(2026, 3, 6, 15, 0);
        var evening = Instant.FromUtc(2026, 3, 6, 23, 0);
        var earlyUtc = Instant.FromUtc(2026, 3, 7, 3, 0);

        // Act
        var first = kpi.AddMeasurement(Taken(40, KpiMeasurementTime.AtMoment(morning), Jan1), Zone, Actor, Now);
        var second = kpi.AddMeasurement(Taken(42, KpiMeasurementTime.AtMoment(evening), Jan1), Zone, Actor, Now);
        var third = kpi.AddMeasurement(Taken(41, KpiMeasurementTime.AtMoment(earlyUtc), Jan1), Zone, Actor, Now);

        // Assert
        first.IsSuccess.Should().BeTrue(first.IsFailure ? first.Error : null);
        second.IsSuccess.Should().BeTrue(second.IsFailure ? second.Error : null);
        third.IsSuccess.Should().BeTrue(third.IsFailure ? third.Error : null);
        var march6 = new LocalDate(2026, 3, 6);
        first.Value.Period.Should().Be(new KpiPeriod(march6, march6));
        third.Value.Period.Should().Be(new KpiPeriod(march6, march6), "9pm in Chicago is still the 6th there");
        third.Value.MeasuredAt.Should().Be(earlyUtc);
        kpi.LatestMeasurement()!.Value.Should().Be(41);
    }

    [Fact]
    public void AddMeasurement_ShouldFail_ForASecondPointInTimeReadingAtTheSameMoment()
    {
        // Arrange
        var kpi = WithWindow(KpiMeasurementWindow.PointInTime);
        var at = Instant.FromUtc(2026, 3, 6, 15, 0);
        kpi.AddMeasurement(Taken(40, KpiMeasurementTime.AtMoment(at), Jan1), Zone, Actor, Now);

        // Act
        var result = kpi.AddMeasurement(Taken(41, KpiMeasurementTime.AtMoment(at), Jan1), Zone, Actor, Now);

        // Assert
        result.Error.Should().Be(KpiErrors.MomentTaken);
    }

    [Fact]
    public void AddMeasurement_ShouldFail_ForAMomentTheDatabaseCannotTellApartFromAnother()
    {
        // Arrange
        var kpi = WithWindow(KpiMeasurementWindow.PointInTime);
        var at = Instant.FromUtc(2026, 3, 6, 15, 0);
        kpi.AddMeasurement(Taken(40, KpiMeasurementTime.AtMoment(at.PlusNanoseconds(10)), Jan1), Zone, Actor, Now);

        // Act
        var result = kpi.AddMeasurement(Taken(41, KpiMeasurementTime.AtMoment(at.PlusNanoseconds(90)), Jan1), Zone, Actor, Now);

        // Assert
        result.Error.Should().Be(KpiErrors.MomentTaken);
        kpi.Measurements.Single().MeasuredAt.Should().Be(at, "a moment is kept to the 100-nanosecond ticks the database stores");
    }

    [Fact]
    public void ChangeMeasurement_ShouldKeepAPointInTimeReadingsDay_WhenTheZoneChangedButTheMomentDidNot()
    {
        // Arrange
        var kpi = WithWindow(KpiMeasurementWindow.PointInTime);
        var at = Instant.FromUtc(2026, 3, 7, 3, 0);
        var measurement = kpi.AddMeasurement(Taken(40, KpiMeasurementTime.AtMoment(at), Jan1), Zone, Actor, Now).Value;
        var tokyo = DateTimeZoneProviders.Tzdb["Asia/Tokyo"];

        // Act
        var result = kpi.ChangeMeasurement(measurement.Id, Taken(41, KpiMeasurementTime.AtMoment(at), Jan1), tokyo, Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var march6 = new LocalDate(2026, 3, 6);
        measurement.Period.Should().Be(new KpiPeriod(march6, march6), "the day it was stored with in Chicago stays");
        measurement.Value.Should().Be(41);
    }

    [Fact]
    public void ChangeMeasurement_ShouldReadANewMomentsDayInTheCurrentZone()
    {
        // Arrange
        var kpi = WithWindow(KpiMeasurementWindow.PointInTime);
        var measurement = kpi.AddMeasurement(Taken(40, KpiMeasurementTime.AtMoment(Instant.FromUtc(2026, 3, 6, 15, 0)), Jan1), Zone, Actor, Now).Value;
        var tokyo = DateTimeZoneProviders.Tzdb["Asia/Tokyo"];

        // Act
        var result = kpi.ChangeMeasurement(measurement.Id, Taken(40, KpiMeasurementTime.AtMoment(Instant.FromUtc(2026, 3, 6, 16, 0)), Jan1), tokyo, Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var march7 = new LocalDate(2026, 3, 7);
        measurement.Period.Should().Be(new KpiPeriod(march7, march7), "1am on the 7th in Tokyo");
    }

    [Theory]
    [MemberData(nameof(MismatchedTimes))]
    public void AddMeasurement_ShouldFail_WhenTheTimeIsNotTheOneTheWindowTakes(KpiMeasurementWindow window, KpiMeasurementTime time, string error)
    {
        // Arrange
        var kpi = WithWindow(window);

        // Act
        var result = kpi.AddMeasurement(Taken(1, time, Jan1), Zone, Actor, Now);

        // Assert
        result.Error.Should().Be(error);
    }

    public static TheoryData<KpiMeasurementWindow, KpiMeasurementTime, string> MismatchedTimes() => new()
    {
        { KpiMeasurementWindow.PerPeriod, KpiMeasurementTime.AsOfDate(Jan1), KpiErrors.PeriodRequired },
        { KpiMeasurementWindow.Cumulative(Jan1), KpiMeasurementTime.ForPeriod(new KpiPeriod(Jan1, Jan1)), KpiErrors.AsOfDateRequired },
        { KpiMeasurementWindow.Rolling(7, KpiRollingUnit.Days), KpiMeasurementTime.AtMoment(Now), KpiErrors.AsOfDateRequired },
        { KpiMeasurementWindow.PointInTime, KpiMeasurementTime.AsOfDate(Jan1), KpiErrors.MomentRequired },
        { KpiMeasurementWindow.PointInTime, new KpiMeasurementTime(null, Jan1, Now), KpiErrors.MomentRequired },
    };

    [Fact]
    public void AddMeasurement_ShouldFail_ForAPeriodRecordOnAWindowThatIsNotPerPeriod()
    {
        // Arrange
        var kpi = WithWindow(KpiMeasurementWindow.Cumulative(Jan1));
        var input = Taken(1, KpiMeasurementTime.AsOfDate(Jan1), Jan1) with { PeriodRecordKind = KpiPeriodRecordKind.Sprint, PeriodRecordId = Guid.NewGuid() };

        // Act
        var result = kpi.AddMeasurement(input, Zone, Actor, Now);

        // Assert
        result.Error.Should().Be(KpiErrors.PeriodRecordOnlyPerPeriod);
    }

    [Fact]
    public void ChangeMeasurementWindow_ShouldChangeIt_AndRaiseBothEnds_WhileTheKpiHasNoMeasurements()
    {
        // Arrange
        var kpi = _faker.Generate();
        var window = KpiMeasurementWindow.Rolling(90, KpiRollingUnit.Days);

        // Act
        var result = kpi.ChangeMeasurementWindow(window, Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        kpi.Window.Should().Be(window);
        var changed = kpi.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<KpiMeasurementWindowChangedEvent>().Subject;
        changed.Window.Should().Be(window);
        changed.Previous.Should().Be(KpiMeasurementWindow.PerPeriod);
    }

    [Fact]
    public void ChangeMeasurementWindow_ShouldRaiseNothing_WhenTheWindowIsTheSame()
    {
        // Arrange
        var kpi = _faker.Generate();

        // Act
        var result = kpi.ChangeMeasurementWindow(KpiMeasurementWindow.PerPeriod, Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        kpi.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeMeasurementWindow_ShouldFail_OnceTheKpiHasMeasurements()
    {
        // Arrange
        var kpi = _faker.Generate();
        kpi.AddMeasurement(Measurement(14, Jan1, Jan1), Zone, Actor, Now);
        kpi.ClearDomainEvents();

        // Act
        var result = kpi.ChangeMeasurementWindow(KpiMeasurementWindow.PointInTime, Actor, Now);

        // Assert
        result.Error.Should().Be(KpiErrors.WindowLockedByMeasurements);
        kpi.Window.Should().Be(KpiMeasurementWindow.PerPeriod);
        kpi.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeMeasurementWindow_ShouldFail_AwayFromPerPeriod_WhileTheKpiReadsARollingAverage()
    {
        // Arrange
        var kpi = Kpi.Create(KpiFaker.TestSubject.Key, Guid.NewGuid(), Definition(rollingAverageOf: 7), 0, Actor, Now).Value;

        // Act
        var result = kpi.ChangeMeasurementWindow(KpiMeasurementWindow.PointInTime, Actor, Now);

        // Assert
        result.Error.Should().Be(KpiErrors.RollingAverageOnlyPerPeriod);
    }

    [Fact]
    public void UpdateDetails_ShouldSetARollingAverage_AndRaiseDetailsUpdated()
    {
        // Arrange
        var kpi = _faker.Generate();
        var details = kpi.Details;

        // Act
        var result = kpi.UpdateDetails(
            new KpiDetailsInput(details.Name, details.Description, details.CategoryId, details.Prefix, details.Suffix, details.ExpressedAs, 7),
            Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        kpi.RollingAverageOf.Should().Be(7);
        var updated = kpi.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<KpiDetailsUpdatedEvent>().Subject;
        updated.Details.RollingAverageOf.Should().Be(7);
        updated.Previous.RollingAverageOf.Should().BeNull();
    }

    private static readonly KpiCheckpointValues Q1 = new(new LocalDate(2026, 3, 31), "Q1", 12, 11);
    private static readonly KpiCheckpointValues Q2 = new(new LocalDate(2026, 6, 30), "Q2", 15, null);

    [Fact]
    public void ChangeCheckpointPlan_ShouldAddThePlanInDateOrder_AndRaiseBothEnds()
    {
        // Arrange
        var kpi = _faker.Generate();

        // Act
        var result = kpi.ChangeCheckpointPlan([Q2, Q1 with { Label = " Q1 " }], Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        kpi.CheckpointPlan.Should().Equal(Q1, Q2);
        kpi.Checkpoints.Should().OnlyContain(c => c.KpiId == kpi.Id);
        var changed = kpi.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<KpiCheckpointPlanChangedEvent>().Subject;
        changed.Plan.Should().Equal(Q1, Q2);
        changed.Added.Should().Equal(Q1, Q2);
        changed.Removed.Should().BeEmpty();
        changed.Revised.Should().BeEmpty();
        changed.RelatedAggregates.Should().Equal(new AggregateReference("Gadget", kpi.SubjectId));
    }

    [Fact]
    public void ChangeCheckpointPlan_ShouldChangeADayAlreadyPlanned_AddANewDay_AndRemoveADayLeftOut()
    {
        // Arrange
        var kpi = _faker.Generate();
        kpi.ChangeCheckpointPlan([Q1, Q2], Actor, Now);
        var keptId = kpi.Checkpoints.Single(c => c.Date == Q1.Date).Id;
        kpi.ClearDomainEvents();
        var q3 = new KpiCheckpointValues(new LocalDate(2026, 9, 30), "Q3", 18, 16);

        // Act
        var result = kpi.ChangeCheckpointPlan([Q1 with { PlannedValue = 13 }, q3], Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        kpi.CheckpointPlan.Should().Equal(Q1 with { PlannedValue = 13 }, q3);
        kpi.Checkpoints.Single(c => c.Date == Q1.Date).Id.Should().Be(keptId);
        var changed = kpi.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<KpiCheckpointPlanChangedEvent>().Subject;
        changed.Added.Should().Equal(q3);
        changed.Removed.Should().Equal(Q2);
        changed.Revised.Should().Equal(new KpiCheckpointRevision(Q1, Q1 with { PlannedValue = 13 }));
        changed.Plan.Should().Equal(Q1 with { PlannedValue = 13 }, q3);
    }

    [Fact]
    public void ChangeCheckpointPlan_ShouldThrow_WhenTheKpiWasLoadedWithoutItsPlan()
    {
        // Arrange
        var kpi = _faker.WithUnloadedCheckpoints().Generate();

        // Act
        var act = () => kpi.ChangeCheckpointPlan([Q1], Actor, Now);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*loaded without its checkpoint plan*");
    }

    [Fact]
    public void ChangeGoal_ShouldThrow_OnADirectionChange_WhenTheKpiWasLoadedWithoutItsPlan()
    {
        // Arrange
        var kpi = _faker.WithUnloadedCheckpoints().Generate();
        var reversed = new KpiGoal(KpiTargetDirection.Decrease, new KpiBaseline(30, null), KpiTarget.Absolute(20), null);

        // Act
        var act = () => kpi.ChangeGoal(reversed, Actor, Now);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ChangeCheckpointPlan_ShouldClearThePlan_WhenGivenNone()
    {
        // Arrange
        var kpi = _faker.Generate();
        kpi.ChangeCheckpointPlan([Q1], Actor, Now);
        kpi.ClearDomainEvents();

        // Act
        var result = kpi.ChangeCheckpointPlan([], Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        kpi.Checkpoints.Should().BeEmpty();
        kpi.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<KpiCheckpointPlanChangedEvent>()
            .Which.Plan.Should().BeEmpty();
    }

    [Fact]
    public void ChangeCheckpointPlan_ShouldRaiseNothing_WhenThePlanIsTheSame()
    {
        // Arrange
        var kpi = _faker.Generate();
        kpi.ChangeCheckpointPlan([Q1, Q2], Actor, Now);
        kpi.ClearDomainEvents();

        // Act
        var result = kpi.ChangeCheckpointPlan([Q2, Q1 with { Label = "Q1 " }], Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        kpi.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeCheckpointPlan_ShouldFail_AndChangeNothing_WhenThePlanDoesNotHold()
    {
        // Arrange
        var kpi = _faker.Generate();
        kpi.ChangeCheckpointPlan([Q1], Actor, Now);
        kpi.ClearDomainEvents();

        // Act
        var result = kpi.ChangeCheckpointPlan([Q1, Q2 with { AtRiskValue = 15 }], Actor, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(KpiErrors.CheckpointAtRiskNotShortIncrease);
        kpi.CheckpointPlan.Should().Equal(Q1);
        kpi.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeCheckpointPlan_ShouldFail_OnAClosedKpi()
    {
        // Arrange
        var kpi = _faker.WithStatus(KpiStatus.Closed).Generate();

        // Act
        var result = kpi.ChangeCheckpointPlan([Q1], Actor, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(KpiErrors.PlanLockedClosed);
        kpi.Checkpoints.Should().BeEmpty();
    }

    [Fact]
    public void ChangeGoal_ShouldFail_WhenANewDirectionPutsACheckpointsAtRiskValueOnTheWrongSide()
    {
        // Arrange
        var kpi = _faker.Generate();
        kpi.ChangeCheckpointPlan([Q1], Actor, Now);
        kpi.ClearDomainEvents();
        var previous = kpi.Goal;
        var reversed = new KpiGoal(KpiTargetDirection.Decrease, new KpiBaseline(30, null), KpiTarget.Absolute(20), null);

        // Act
        var result = kpi.ChangeGoal(reversed, Actor, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(KpiErrors.DirectionConflictsWithPlan);
        kpi.Goal.Should().Be(previous);
        kpi.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeGoal_ShouldReverseTheDirection_WhenNoCheckpointHasAnAtRiskValue()
    {
        // Arrange
        var kpi = _faker.Generate();
        kpi.ChangeCheckpointPlan([Q2], Actor, Now);
        var reversed = new KpiGoal(KpiTargetDirection.Decrease, new KpiBaseline(30, null), KpiTarget.Absolute(20), null);

        // Act
        var result = kpi.ChangeGoal(reversed, Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        kpi.Direction.Should().Be(KpiTargetDirection.Decrease);
    }

    [Fact]
    public void CheckpointFor_ShouldBeTheCheckpointAPeriodEndFallsUnder_OrNullPastThePlan()
    {
        // Arrange
        var kpi = _faker.Generate();
        kpi.ChangeCheckpointPlan([Q1, Q2], Actor, Now);

        // Act
        var april = kpi.CheckpointFor(new LocalDate(2026, 4, 30));
        var july = kpi.CheckpointFor(new LocalDate(2026, 7, 31));

        // Assert
        april.Should().Be(Q2);
        july.Should().BeNull();
    }
}
