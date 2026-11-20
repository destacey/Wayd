using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Kpis;
using Wayd.Common.Models;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Kpis;

/// <summary>
/// The KPI tables against real SQL Server: what the in-memory fakes cannot see — the goal's and window's flattened
/// columns read back into their records, the measurements and checkpoints loaded through their field-backed
/// navigations, the period and checkpoint-date indexes, the cascade from a KPI to its measurements and checkpoints,
/// and the category key that refuses a delete in use.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class KpiPersistenceIntegrationTests(SqlServerDbContextFixture fixture)
{
    private const string GadgetType = "test.kpi-persistence.gadget";
    private static readonly KpiSubjectTypeDescriptor Gadget = new(GadgetType, "Gadget", "Gadget");
    private static readonly Instant Timestamp = Instant.FromUtc(2026, 11, 16, 9, 30);
    private static readonly EventActor Actor = EventActor.System;
    private static readonly LocalDate Jan1 = new(2026, 1, 1);
    private static readonly DateTimeZone Zone = DateTimeZoneProviders.Tzdb["America/Chicago"];

    static KpiPersistenceIntegrationTests()
    {
        KpiSubjectTypes.Register(Gadget);
    }

    [Fact]
    public async Task Save_ShouldKeepTheGoalAndMeasurements()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var employee = await SeedEmployee();
        var category = await SeedCategory();
        var window = new KpiPeriod(new LocalDate(2025, 10, 1), new LocalDate(2025, 12, 31));
        var goal = new KpiGoal(
            KpiTargetDirection.Decrease,
            new KpiBaseline(20, window),
            KpiTarget.RelativeChange(25),
            KpiAtRiskThreshold.ProgressToTarget(60));
        Guid kpiId;
        await using (var context = fixture.CreateContext())
        {
            var kpi = Kpi.Create(GadgetType, Guid.CreateVersion7(), Definition(goal, category.Id), 0, Actor, Timestamp).Value;
            kpi.AddMeasurement(Measurement(18, Jan1, new LocalDate(2026, 1, 31), employee.Id) with
            {
                PeriodRecordKind = KpiPeriodRecordKind.Sprint,
                PeriodRecordId = Guid.CreateVersion7(),
                SampleSize = 9,
                Note = "January",
            }, Zone, Actor, Timestamp);
            context.Kpis.Add(kpi);
            kpiId = kpi.Id;

            // Act
            await context.SaveChangesAsync(cancellationToken);
        }

        // Assert
        await using var reader = fixture.CreateContext();
        var stored = await reader.Kpis.Include(k => k.Measurements).SingleAsync(k => k.Id == kpiId, cancellationToken);
        stored.Key.Should().BePositive();
        stored.Goal.Should().Be(goal);
        stored.Goal.TargetValue.Should().Be(15);
        stored.CategoryId.Should().Be(category.Id);
        stored.Status.Should().Be(KpiStatus.Active);
        var measurement = stored.Measurements.Should().ContainSingle().Subject;
        measurement.Period.Should().Be(new KpiPeriod(Jan1, new LocalDate(2026, 1, 31)));
        measurement.PeriodRecordKind.Should().Be(KpiPeriodRecordKind.Sprint);
        measurement.MeasuredById.Should().Be(employee.Id);
        measurement.SampleSize.Should().Be(9);
        measurement.Note.Should().Be("January");
    }

    [Fact]
    public async Task Save_ShouldReadAGoalWithNoBaselineOrThresholdAsNulls()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var goal = new KpiGoal(KpiTargetDirection.Increase, KpiBaseline.None, KpiTarget.Absolute(5), null);
        Guid kpiId;
        await using (var context = fixture.CreateContext())
        {
            var kpi = Kpi.Create(GadgetType, Guid.CreateVersion7(), Definition(goal, null), 0, Actor, Timestamp).Value;
            context.Kpis.Add(kpi);
            kpiId = kpi.Id;

            // Act
            await context.SaveChangesAsync(cancellationToken);
        }

        // Assert
        await using var reader = fixture.CreateContext();
        var stored = await reader.Kpis.SingleAsync(k => k.Id == kpiId, cancellationToken);
        stored.Goal.Should().Be(goal);
        stored.AtRisk.Should().BeNull();
        stored.Baseline.Window.Should().BeNull();
    }

    [Fact]
    public async Task Save_ShouldRefuseTwoMeasurementsEndingTheSameDay_FromConcurrentSaves()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var kpiId = await SeedKpi();
        await using var first = fixture.CreateContext();
        await using var second = fixture.CreateContext();
        var firstKpi = await first.Kpis.Include(k => k.Measurements).SingleAsync(k => k.Id == kpiId, cancellationToken);
        var secondKpi = await second.Kpis.Include(k => k.Measurements).SingleAsync(k => k.Id == kpiId, cancellationToken);
        firstKpi.AddMeasurement(Measurement(14, Jan1, new LocalDate(2026, 1, 31), null), Zone, Actor, Timestamp).IsSuccess.Should().BeTrue();
        secondKpi.AddMeasurement(Measurement(15, new LocalDate(2026, 1, 15), new LocalDate(2026, 1, 31), null), Zone, Actor, Timestamp).IsSuccess.Should().BeTrue();
        await first.SaveChangesAsync(cancellationToken);

        // Act
        var act = () => second.SaveChangesAsync(cancellationToken);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Save_ShouldRefuseOverlappingMeasurementsStartingOnDifferentDays_WhenEachSaveTouchesTheKpi()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var kpiId = await SeedKpi();
        await using var first = fixture.CreateContext();
        await using var second = fixture.CreateContext();
        var firstKpi = await first.Kpis.Include(k => k.Measurements).SingleAsync(k => k.Id == kpiId, cancellationToken);
        var secondKpi = await second.Kpis.Include(k => k.Measurements).SingleAsync(k => k.Id == kpiId, cancellationToken);
        firstKpi.AddMeasurement(Measurement(14, Jan1, new LocalDate(2026, 1, 31), null), Zone, Actor, Timestamp).IsSuccess.Should().BeTrue();
        secondKpi.AddMeasurement(Measurement(15, new LocalDate(2026, 1, 15), new LocalDate(2026, 2, 14), null), Zone, Actor, Timestamp).IsSuccess.Should().BeTrue();
        first.TouchKpi(firstKpi);
        second.TouchKpi(secondKpi);
        await first.SaveChangesAsync(cancellationToken);

        // Act
        var act = () => second.SaveChangesAsync(cancellationToken);

        // Assert
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task Save_ShouldKeepTheWindowAndRollingAverage()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var cumulative = KpiMeasurementWindow.Cumulative(new LocalDate(2026, 2, 16));
        var rolling = KpiMeasurementWindow.Rolling(90, KpiRollingUnit.Days);
        Guid cumulativeId, rollingId, averagedId;
        await using (var context = fixture.CreateContext())
        {
            var cumulativeKpi = Kpi.Create(GadgetType, Guid.CreateVersion7(), Definition(DefaultGoal, null, cumulative), 0, Actor, Timestamp).Value;
            var rollingKpi = Kpi.Create(GadgetType, Guid.CreateVersion7(), Definition(DefaultGoal, null, rolling), 0, Actor, Timestamp).Value;
            var averagedKpi = Kpi.Create(GadgetType, Guid.CreateVersion7(), Definition(DefaultGoal, null, rollingAverageOf: 7), 0, Actor, Timestamp).Value;
            context.Kpis.AddRange(cumulativeKpi, rollingKpi, averagedKpi);
            (cumulativeId, rollingId, averagedId) = (cumulativeKpi.Id, rollingKpi.Id, averagedKpi.Id);

            // Act
            await context.SaveChangesAsync(cancellationToken);
        }

        // Assert
        await using var reader = fixture.CreateContext();
        (await reader.Kpis.SingleAsync(k => k.Id == cumulativeId, cancellationToken)).Window.Should().Be(cumulative);
        (await reader.Kpis.SingleAsync(k => k.Id == rollingId, cancellationToken)).Window.Should().Be(rolling);
        var averaged = await reader.Kpis.SingleAsync(k => k.Id == averagedId, cancellationToken);
        averaged.Window.Should().Be(KpiMeasurementWindow.PerPeriod);
        averaged.RollingAverageOf.Should().Be(7);
    }

    [Fact]
    public async Task Save_ShouldKeepSeveralPointInTimeReadingsOnOneDay_ByTheirMoments()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var morning = Instant.FromUtc(2026, 3, 6, 15, 0);
        var evening = Instant.FromUtc(2026, 3, 6, 23, 0);
        Guid kpiId;
        await using (var context = fixture.CreateContext())
        {
            var kpi = Kpi.Create(GadgetType, Guid.CreateVersion7(), Definition(DefaultGoal, null, KpiMeasurementWindow.PointInTime), 0, Actor, Timestamp).Value;
            kpi.AddMeasurement(AtMoment(40, morning), Zone, Actor, Timestamp).IsSuccess.Should().BeTrue();
            kpi.AddMeasurement(AtMoment(42, evening), Zone, Actor, Timestamp).IsSuccess.Should().BeTrue();
            context.Kpis.Add(kpi);
            kpiId = kpi.Id;

            // Act
            await context.SaveChangesAsync(cancellationToken);
        }

        // Assert
        await using var reader = fixture.CreateContext();
        var stored = await reader.Kpis.Include(k => k.Measurements).SingleAsync(k => k.Id == kpiId, cancellationToken);
        stored.Measurements.Select(m => m.MeasuredAt).Should().BeEquivalentTo(new Instant?[] { morning, evening });
        stored.LatestMeasurement()!.Value.Should().Be(42);
    }

    [Fact]
    public async Task Save_ShouldRefuseTwoCumulativeValuesAsOfTheSameDay_FromConcurrentSaves()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var kpiId = await SeedKpi(window: KpiMeasurementWindow.Cumulative(Jan1));
        await using var first = fixture.CreateContext();
        await using var second = fixture.CreateContext();
        var firstKpi = await first.Kpis.Include(k => k.Measurements).SingleAsync(k => k.Id == kpiId, cancellationToken);
        var secondKpi = await second.Kpis.Include(k => k.Measurements).SingleAsync(k => k.Id == kpiId, cancellationToken);
        var asOf = new LocalDate(2026, 2, 6);
        firstKpi.AddMeasurement(AsOf(14, asOf), Zone, Actor, Timestamp).IsSuccess.Should().BeTrue();
        secondKpi.AddMeasurement(AsOf(15, asOf), Zone, Actor, Timestamp).IsSuccess.Should().BeTrue();
        await first.SaveChangesAsync(cancellationToken);

        // Act
        var act = () => second.SaveChangesAsync(cancellationToken);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task RemovingAKpi_ShouldRemoveItsMeasurements()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var kpiId = await SeedKpi(withMeasurement: true);
        await using (var context = fixture.CreateContext())
        {
            var kpi = await context.Kpis.SingleAsync(k => k.Id == kpiId, cancellationToken);
            kpi.Remove(Actor, Timestamp);
            context.Kpis.Remove(kpi);

            // Act
            await context.SaveChangesAsync(cancellationToken);
        }

        // Assert
        await using var reader = fixture.CreateContext();
        (await reader.Kpis.AnyAsync(k => k.Id == kpiId, cancellationToken)).Should().BeFalse();
        (await reader.KpiMeasurements.AnyAsync(m => m.KpiId == kpiId, cancellationToken)).Should().BeFalse();
    }

    [Fact]
    public async Task Save_ShouldReplaceThePlan_ChangingRemovingAndAddingCheckpointsInOneSave()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var kpiId = await SeedKpi(plan: [Q1, Q2]);
        var q3 = new KpiCheckpointValues(new LocalDate(2026, 9, 30), "Q3", 18, null);
        var q1Changed = Q1 with { Label = "Spring", PlannedValue = 13 };
        await using (var context = fixture.CreateContext())
        {
            var kpi = await context.Kpis.Include(k => k.Checkpoints).SingleAsync(k => k.Id == kpiId, cancellationToken);
            kpi.ChangeCheckpointPlan([q3, q1Changed], Actor, Timestamp).IsSuccess.Should().BeTrue();
            context.TouchKpi(kpi);

            // Act
            await context.SaveChangesAsync(cancellationToken);
        }

        // Assert
        await using var reader = fixture.CreateContext();
        var stored = await reader.Kpis.Include(k => k.Checkpoints).SingleAsync(k => k.Id == kpiId, cancellationToken);
        stored.CheckpointPlan.Should().Equal(q1Changed, q3);
    }

    [Fact]
    public async Task Load_ShouldReadAnIncludedPlanThatIsEmpty_AsNoPlan_TrackedOrNot()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var kpiId = await SeedKpi();
        await using var context = fixture.CreateContext();

        // Act
        var tracked = await context.Kpis.Include(k => k.Checkpoints).SingleAsync(k => k.Id == kpiId, cancellationToken);
        var untracked = await context.Kpis.AsNoTracking().Include(k => k.Checkpoints).SingleAsync(k => k.Id == kpiId, cancellationToken);

        // Assert
        tracked.CheckpointPlan.Should().BeEmpty();
        untracked.CheckpointPlan.Should().BeEmpty();
        tracked.ChangeCheckpointPlan([Q1], Actor, Timestamp).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Load_ShouldRefuseAPlanChange_WhenThePlanWasNotIncluded()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var kpiId = await SeedKpi(plan: [Q1]);
        await using var context = fixture.CreateContext();
        var kpi = await context.Kpis.SingleAsync(k => k.Id == kpiId, cancellationToken);

        // Act
        var act = () => kpi.ChangeCheckpointPlan([Q1, Q2], Actor, Timestamp);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*loaded without its checkpoint plan*");
    }

    [Fact]
    public async Task Save_ShouldRefuseTwoCheckpointsOnTheSameDay_FromConcurrentSaves()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var kpiId = await SeedKpi();
        await using var first = fixture.CreateContext();
        await using var second = fixture.CreateContext();
        var firstKpi = await first.Kpis.Include(k => k.Checkpoints).SingleAsync(k => k.Id == kpiId, cancellationToken);
        var secondKpi = await second.Kpis.Include(k => k.Checkpoints).SingleAsync(k => k.Id == kpiId, cancellationToken);
        firstKpi.ChangeCheckpointPlan([Q1], Actor, Timestamp).IsSuccess.Should().BeTrue();
        secondKpi.ChangeCheckpointPlan([Q1 with { Label = "Other" }], Actor, Timestamp).IsSuccess.Should().BeTrue();
        await first.SaveChangesAsync(cancellationToken);

        // Act
        var act = () => second.SaveChangesAsync(cancellationToken);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Save_ShouldRefuseAConcurrentPlanChange_WhenEachSaveTouchesTheKpi()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var kpiId = await SeedKpi(plan: [Q1]);
        await using var first = fixture.CreateContext();
        await using var second = fixture.CreateContext();
        var firstKpi = await first.Kpis.Include(k => k.Checkpoints).SingleAsync(k => k.Id == kpiId, cancellationToken);
        var secondKpi = await second.Kpis.Include(k => k.Checkpoints).SingleAsync(k => k.Id == kpiId, cancellationToken);
        firstKpi.ChangeCheckpointPlan([Q1, Q2], Actor, Timestamp).IsSuccess.Should().BeTrue();
        secondKpi.ChangeCheckpointPlan([Q1 with { PlannedValue = 13 }], Actor, Timestamp).IsSuccess.Should().BeTrue();
        first.TouchKpi(firstKpi);
        second.TouchKpi(secondKpi);
        await first.SaveChangesAsync(cancellationToken);

        // Act
        var act = () => second.SaveChangesAsync(cancellationToken);

        // Assert
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task RemovingAKpi_ShouldRemoveItsCheckpoints()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var kpiId = await SeedKpi(plan: [Q1, Q2]);
        await using (var context = fixture.CreateContext())
        {
            var kpi = await context.Kpis.SingleAsync(k => k.Id == kpiId, cancellationToken);
            kpi.Remove(Actor, Timestamp);
            context.Kpis.Remove(kpi);

            // Act
            await context.SaveChangesAsync(cancellationToken);
        }

        // Assert
        await using var reader = fixture.CreateContext();
        (await reader.Set<KpiCheckpoint>().AnyAsync(c => c.KpiId == kpiId, cancellationToken)).Should().BeFalse();
    }

    [Fact]
    public async Task DeletingACategoryInUse_ShouldBeRefused()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var category = await SeedCategory();
        await using (var seed = fixture.CreateContext())
        {
            seed.Kpis.Add(Kpi.Create(GadgetType, Guid.CreateVersion7(), Definition(DefaultGoal, category.Id), 0, Actor, Timestamp).Value);
            await seed.SaveChangesAsync(cancellationToken);
        }

        await using var context = fixture.CreateContext();
        var stored = await context.KpiCategories.SingleAsync(c => c.Id == category.Id, cancellationToken);
        stored.Delete(Actor, Timestamp);
        context.KpiCategories.Remove(stored);

        // Act
        var act = () => context.SaveChangesAsync(cancellationToken);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Save_ShouldRefuseACategoryNameThatDiffersOnlyInCase()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var category = await SeedCategory();
        await using var context = fixture.CreateContext();
        context.KpiCategories.Add(KpiCategory.Create(category.Name.ToUpperInvariant(), null, 1, Actor, Timestamp).Value);

        // Act
        var act = () => context.SaveChangesAsync(cancellationToken);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    private static readonly KpiGoal DefaultGoal =
        new(KpiTargetDirection.Increase, new KpiBaseline(10, null), KpiTarget.Absolute(20), null);

    private static KpiDefinition Definition(KpiGoal goal, Guid? categoryId, KpiMeasurementWindow? window = null, int? rollingAverageOf = null) =>
        new("Throughput", null, categoryId, null, "items", KpiExpressedAs.Value, goal, window, rollingAverageOf);

    private static KpiMeasurementInput Measurement(double value, LocalDate start, LocalDate end, Guid? measuredById) =>
        new(value, KpiMeasurementTime.ForPeriod(new KpiPeriod(start, end)), null, null, end, measuredById, KpiMeasurementSource.Manual, null, null);

    private static KpiMeasurementInput AsOf(double value, LocalDate asOf) =>
        new(value, KpiMeasurementTime.AsOfDate(asOf), null, null, asOf, null, KpiMeasurementSource.Manual, null, null);

    private static KpiMeasurementInput AtMoment(double value, Instant at) =>
        new(value, KpiMeasurementTime.AtMoment(at), null, null, at.InZone(Zone).Date, null, KpiMeasurementSource.Manual, null, null);

    private static readonly KpiCheckpointValues Q1 = new(new LocalDate(2026, 3, 31), "Q1", 12, 11);
    private static readonly KpiCheckpointValues Q2 = new(new LocalDate(2026, 6, 30), "Q2", 15, null);

    private async Task<Guid> SeedKpi(bool withMeasurement = false, KpiMeasurementWindow? window = null, KpiCheckpointValues[]? plan = null)
    {
        var kpi = Kpi.Create(GadgetType, Guid.CreateVersion7(), Definition(DefaultGoal, null, window), 0, Actor, Timestamp).Value;
        if (withMeasurement)
            kpi.AddMeasurement(Measurement(12, Jan1, new LocalDate(2026, 1, 31), null), Zone, Actor, Timestamp);
        if (plan is not null)
            kpi.ChangeCheckpointPlan(plan, Actor, Timestamp).IsSuccess.Should().BeTrue();

        await using WaydDbContext context = fixture.CreateContext();
        context.Kpis.Add(kpi);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return kpi.Id;
    }

    private async Task<KpiCategory> SeedCategory()
    {
        var category = KpiCategory.Create($"Flow {Guid.NewGuid():N}"[..20], null, 0, Actor, Timestamp).Value;

        await using WaydDbContext context = fixture.CreateContext();
        context.KpiCategories.Add(category);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return category;
    }

    private async Task<Employee> SeedEmployee()
    {
        var suffix = $"{Guid.CreateVersion7():N}"[..12];
        var employee = Employee.Create(
            new PersonName("Avery", null, "Chen"),
            suffix,
            Timestamp,
            new EmailAddress($"{suffix}@acme.example"),
            jobTitle: null,
            department: null,
            officeLocation: null,
            managerId: null,
            isActive: true,
            employeeType: null,
            EventActor.System,
            Timestamp);

        await using WaydDbContext context = fixture.CreateContext();
        context.Employees.Add(employee);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return employee;
    }
}
