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

    private readonly KpiFaker _faker = new();

    private static KpiDefinition Definition(KpiGoal? goal = null) => new(
        " Cycle time ",
        "Days from start to done.",
        null,
        null,
        " days ",
        KpiExpressedAs.Value,
        goal ?? new KpiGoal(KpiTargetDirection.Decrease, new KpiBaseline(20, null), KpiTarget.RelativeChange(25), null));

    private static KpiMeasurementInput Measurement(double value, LocalDate start, LocalDate end) =>
        new(value, new KpiPeriod(start, end), null, null, end, Guid.NewGuid(), KpiMeasurementSource.Manual, null, null);

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
        kpi.AddMeasurement(Measurement(14, Jan1, new LocalDate(2026, 1, 31)), Actor, Now);
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
        var result = kpi.AddMeasurement(input, Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        kpi.Measurements.Should().ContainSingle().Which.Should().BeSameAs(result.Value);
        result.Value.Note.Should().Be("January");
        var added = kpi.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<KpiMeasurementAddedEvent>().Subject;
        added.Measurement.Should().Be(new KpiMeasurementValues(
            result.Value.Id, 14, input.Period, null, null, input.MeasuredOn, input.MeasuredById, KpiMeasurementSource.Manual, 12, "January"));
    }

    [Theory]
    [InlineData(2026, 1, 31, 2026, 2, 28)]
    [InlineData(2025, 12, 1, 2026, 1, 1)]
    [InlineData(2026, 1, 10, 2026, 1, 20)]
    public void AddMeasurement_ShouldFail_WhenThePeriodOverlapsAnother(int sy, int sm, int sd, int ey, int em, int ed)
    {
        // Arrange
        var kpi = _faker.Generate();
        kpi.AddMeasurement(Measurement(14, Jan1, new LocalDate(2026, 1, 31)), Actor, Now);
        kpi.ClearDomainEvents();

        // Act
        var result = kpi.AddMeasurement(Measurement(15, new LocalDate(sy, sm, sd), new LocalDate(ey, em, ed)), Actor, Now);

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
        kpi.AddMeasurement(Measurement(14, Jan1, new LocalDate(2026, 1, 31)), Actor, Now);

        // Act
        var result = kpi.AddMeasurement(Measurement(15, new LocalDate(2026, 2, 1), new LocalDate(2026, 2, 28)), Actor, Now);

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
        var result = kpi.AddMeasurement(Measurement(14, Jan1, Jan1), Actor, Now);

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
        var result = kpi.AddMeasurement(input, Actor, Now);

        // Assert
        result.Error.Should().Be(KpiErrors.PeriodRecordIncomplete);
    }

    [Fact]
    public void ChangeMeasurement_ShouldRaiseBothEnds_AndMayKeepItsOwnPeriod()
    {
        // Arrange
        var kpi = _faker.Generate();
        var measurement = kpi.AddMeasurement(Measurement(14, Jan1, new LocalDate(2026, 1, 31)), Actor, Now).Value;
        var previous = new KpiMeasurementValues(measurement.Id, measurement.Value, measurement.Period, null, null,
            measurement.MeasuredOn, measurement.MeasuredById, measurement.Source, null, null);
        kpi.ClearDomainEvents();
        var input = Measurement(13, Jan1, new LocalDate(2026, 1, 31)) with { MeasuredById = measurement.MeasuredById };

        // Act
        var result = kpi.ChangeMeasurement(measurement.Id, input, Actor, Now);

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
        var january = kpi.AddMeasurement(Measurement(14, Jan1, new LocalDate(2026, 1, 31)), Actor, Now).Value;
        kpi.AddMeasurement(Measurement(15, new LocalDate(2026, 2, 1), new LocalDate(2026, 2, 28)), Actor, Now);
        kpi.ClearDomainEvents();

        // Act
        var result = kpi.ChangeMeasurement(january.Id, Measurement(14, Jan1, new LocalDate(2026, 2, 1)), Actor, Now);

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
        var measurement = kpi.AddMeasurement(Measurement(14, Jan1, Jan1), Actor, Now).Value;
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
        kpi.AddMeasurement(Measurement(15, new LocalDate(2026, 3, 1), new LocalDate(2026, 3, 31)), Actor, Now);
        kpi.AddMeasurement(Measurement(14, Jan1, new LocalDate(2026, 1, 31)), Actor, Now);

        // Act
        var latest = kpi.LatestMeasurement();

        // Assert
        latest!.Value.Should().Be(15);
    }
}
