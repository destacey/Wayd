using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Kpis;
using Wayd.Common.Application.Kpis.Commands;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Tests.Sut.Kpis.Commands;

public sealed class RecordKpiMeasurementCommandHandlerTests
{
    private static readonly LocalDate Jan1 = new(2026, 1, 1);
    private static readonly LocalDate Jan31 = new(2026, 1, 31);

    private readonly KpiTestHarness _harness = new();

    private RecordKpiMeasurementCommandHandler CreateHandler() => new(_harness.Scope());

    private RecordKpiMeasurementCommand Command(Guid kpiId, LocalDate start, LocalDate end, LocalDate? measuredOn = null) =>
        new(KpiTestHarness.Gadget.Key, _harness.SubjectId, kpiId, new KpiMeasurementValuesInput(14, start, end, null, null, measuredOn, 8, "January"));

    [Fact]
    public async Task Handle_ShouldRecordTheValueAsTakenByTheCaller_OnThePeriodsLastDayByDefault()
    {
        // Arrange
        var kpi = _harness.AddKpi();

        // Act
        var result = await CreateHandler().Handle(Command(kpi.Id, Jan1, Jan31), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var measurement = kpi.Measurements.Should().ContainSingle().Subject;
        measurement.Id.Should().Be(result.Value);
        measurement.MeasuredById.Should().Be(_harness.EmployeeId);
        measurement.MeasuredOn.Should().Be(Jan31);
        measurement.Source.Should().Be(KpiMeasurementSource.Manual);
        measurement.SampleSize.Should().Be(8);
        _harness.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldWriteTheKpisRow_SoAConcurrentOverlappingRecordingCollides()
    {
        // Arrange
        var kpi = _harness.AddKpi();

        // Act
        var result = await CreateHandler().Handle(Command(kpi.Id, Jan1, Jan31), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _harness.DbContext.TouchedKpis.Should().ContainSingle().Which.Should().BeSameAs(kpi);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenThePeriodOverlapsAnother()
    {
        // Arrange
        var kpi = _harness.AddKpi();
        kpi.AddMeasurement(KpiTestHarness.Measurement(12, Jan1, Jan31), KpiTestHarness.Zone, EventActor.System, _harness.DateTimeProvider.Now);

        // Act
        var result = await CreateHandler().Handle(Command(kpi.Id, new LocalDate(2026, 1, 15), new LocalDate(2026, 2, 14)), TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(KpiErrors.PeriodOverlaps);
        kpi.Measurements.Should().ContainSingle();
        _harness.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldRecordAPointInTimeMoment_TakenOnItsDayInTheSystemTimeZone()
    {
        // Arrange
        var kpi = _harness.AddKpi(window: KpiMeasurementWindow.PointInTime);
        var at = Instant.FromUtc(2026, 3, 7, 3, 0);
        var command = new RecordKpiMeasurementCommand(KpiTestHarness.Gadget.Key, _harness.SubjectId, kpi.Id,
            new KpiMeasurementValuesInput(41, null, null, null, null, null, null, null, MeasuredAt: at));

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var measurement = kpi.Measurements.Should().ContainSingle().Subject;
        var march6 = new LocalDate(2026, 3, 6);
        measurement.MeasuredAt.Should().Be(at);
        measurement.Period.Should().Be(new KpiPeriod(march6, march6), "the system zone is six hours behind UTC");
        measurement.MeasuredOn.Should().Be(march6);
    }

    [Fact]
    public async Task Handle_ShouldWorkOutACumulativePeriod_FromTheAsOfDay()
    {
        // Arrange
        var anchor = new LocalDate(2026, 2, 16);
        var kpi = _harness.AddKpi(window: KpiMeasurementWindow.Cumulative(anchor));
        var asOf = new LocalDate(2026, 2, 27);
        var command = new RecordKpiMeasurementCommand(KpiTestHarness.Gadget.Key, _harness.SubjectId, kpi.Id,
            new KpiMeasurementValuesInput(9, null, null, null, null, null, null, null, AsOf: asOf));

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var measurement = kpi.Measurements.Should().ContainSingle().Subject;
        measurement.Period.Should().Be(new KpiPeriod(anchor, asOf));
        measurement.MeasuredOn.Should().Be(asOf);
    }

    [Fact]
    public async Task Handle_ShouldFail_ForAPeriodOnACumulativeKpi()
    {
        // Arrange
        var kpi = _harness.AddKpi(window: KpiMeasurementWindow.Cumulative(Jan1));

        // Act
        var result = await CreateHandler().Handle(Command(kpi.Id, Jan1, Jan31), TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(KpiErrors.AsOfDateRequired);
        _harness.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldBeNotFound_ForAKpiOnAnotherRecord()
    {
        // Arrange
        var elsewhere = _harness.AddKpi(subjectId: Guid.NewGuid());

        // Act
        var result = await CreateHandler().Handle(Command(elsewhere.Id, Jan1, Jan31), TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(KpiAccessErrors.NotFound);
        elsewhere.Measurements.Should().BeEmpty();
        _harness.DbContext.SaveChangesCallCount.Should().Be(0);
    }
}