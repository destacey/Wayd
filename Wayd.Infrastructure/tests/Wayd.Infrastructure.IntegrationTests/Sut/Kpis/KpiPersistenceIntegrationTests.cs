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
/// The KPI tables against real SQL Server: what the in-memory fakes cannot see — the goal's flattened columns read
/// back into its records, the measurements loaded through the field-backed navigation, the period index, the cascade
/// from a KPI to its measurements, and the category key that refuses a delete in use.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class KpiPersistenceIntegrationTests(SqlServerDbContextFixture fixture)
{
    private const string GadgetType = "test.kpi-persistence.gadget";
    private static readonly KpiSubjectTypeDescriptor Gadget = new(GadgetType, "Gadget", "Gadget");
    private static readonly Instant Timestamp = Instant.FromUtc(2026, 11, 16, 9, 30);
    private static readonly EventActor Actor = EventActor.System;
    private static readonly LocalDate Jan1 = new(2026, 1, 1);

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
            }, Actor, Timestamp);
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
    public async Task Save_ShouldRefuseTwoMeasurementsStartingTheSameDay_FromConcurrentSaves()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var kpiId = await SeedKpi();
        await using var first = fixture.CreateContext();
        await using var second = fixture.CreateContext();
        var firstKpi = await first.Kpis.Include(k => k.Measurements).SingleAsync(k => k.Id == kpiId, cancellationToken);
        var secondKpi = await second.Kpis.Include(k => k.Measurements).SingleAsync(k => k.Id == kpiId, cancellationToken);
        firstKpi.AddMeasurement(Measurement(14, Jan1, new LocalDate(2026, 1, 31), null), Actor, Timestamp).IsSuccess.Should().BeTrue();
        secondKpi.AddMeasurement(Measurement(15, Jan1, new LocalDate(2026, 1, 15), null), Actor, Timestamp).IsSuccess.Should().BeTrue();
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
        firstKpi.AddMeasurement(Measurement(14, Jan1, new LocalDate(2026, 1, 31), null), Actor, Timestamp).IsSuccess.Should().BeTrue();
        secondKpi.AddMeasurement(Measurement(15, new LocalDate(2026, 1, 15), new LocalDate(2026, 2, 14), null), Actor, Timestamp).IsSuccess.Should().BeTrue();
        first.TouchKpi(firstKpi);
        second.TouchKpi(secondKpi);
        await first.SaveChangesAsync(cancellationToken);

        // Act
        var act = () => second.SaveChangesAsync(cancellationToken);

        // Assert
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
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

    private static KpiDefinition Definition(KpiGoal goal, Guid? categoryId) =>
        new("Throughput", null, categoryId, null, "items", KpiExpressedAs.Value, goal);

    private static KpiMeasurementInput Measurement(double value, LocalDate start, LocalDate end, Guid? measuredById) =>
        new(value, new KpiPeriod(start, end), null, null, end, measuredById, KpiMeasurementSource.Manual, null, null);

    private async Task<Guid> SeedKpi(bool withMeasurement = false)
    {
        var kpi = Kpi.Create(GadgetType, Guid.CreateVersion7(), Definition(DefaultGoal, null), 0, Actor, Timestamp).Value;
        if (withMeasurement)
            kpi.AddMeasurement(Measurement(12, Jan1, new LocalDate(2026, 1, 31), null), Actor, Timestamp);

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
