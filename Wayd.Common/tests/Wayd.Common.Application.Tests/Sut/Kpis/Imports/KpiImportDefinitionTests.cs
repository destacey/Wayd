using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Kpis.Imports;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Imports;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Tests.Sut.Kpis.Imports;

public sealed class KpiImportDefinitionTests
{
    private static readonly Guid ProcessId = Guid.CreateVersion7();
    private static readonly LocalDate Jan1 = new(2025, 1, 1);

    private readonly KpiTestHarness _harness = new();
    private readonly Mock<IFeatureFlags> _featureFlags = new();
    private readonly KpiImportDefinition _definition;

    public KpiImportDefinitionTests()
    {
        _harness.SubjectType
            .Setup(t => t.Describe(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, CancellationToken _) =>
                ids.Where(id => id == _harness.SubjectId)
                    .ToDictionary(id => id, _ => new ImportRecordLink(ImportRecordType.Team, "14", "Cards")));

        _definition = new KpiImportDefinition(
            _harness.DbContext,
            [_harness.SubjectType.Object],
            _featureFlags.Object,
            _harness.DateTimeProvider,
            _harness.CurrentUser.Object,
            _harness.GeneralSettings.Object,
            new ImportPayloadSerializer());
    }

    private ImportKpiDto Dto(
        string name = "Throughput",
        Guid? subjectId = null,
        Guid? categoryId = null,
        bool isClosed = false,
        params ImportKpiMeasurementDto[] measurements) => new(
        KpiTestHarness.Gadget.Key,
        subjectId ?? _harness.SubjectId,
        name,
        null,
        categoryId,
        null,
        "items",
        KpiExpressedAs.ChangeFromBaseline,
        KpiTargetDirection.Increase,
        new KpiBaseline(10, new KpiPeriod(new LocalDate(2024, 10, 1), new LocalDate(2024, 12, 31))),
        KpiTarget.RelativeChange(50),
        KpiAtRiskThreshold.ProgressToTarget(80),
        isClosed,
        measurements);

    private static ImportKpiMeasurementDto Month(int month, double value) =>
        new(value, Jan1.PlusMonths(month), Jan1.PlusMonths(month + 1).PlusDays(-1), null, 20, null);

    private ImportProcessRow Row(int rowNumber, ImportKpiDto dto) =>
        ImportProcessRow.Create($"r{rowNumber}", rowNumber, _definition.SerializeRow(dto));

    private Task<CSharpFunctionalExtensions.Result<ImportPassResult>> RunPass(params ImportProcessRow[] rows) =>
        _definition.ExecutePass(ProcessId, 0, rows, isFinalChunk: true, TestContext.Current.CancellationToken);

    [Fact]
    public async Task CreateKpis_AddsTheKpiWithItsHistory_AfterTheRecordsExistingKpis()
    {
        // Arrange
        _harness.AddKpi(name: "Lead time", order: 3);
        var row = Row(1, Dto(measurements: [Month(1, 13), Month(0, 12)]));

        // Act
        var result = await RunPass(row);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeFalse(outcome.Error);

        var created = _harness.DbContext.Kpis.Single(k => k.Id == outcome.CreatedEntityId);
        created.Order.Should().Be(4);
        created.Goal.TargetValue.Should().Be(15);
        created.Goal.AtRiskValue.Should().Be(14);
        created.Baseline.Window.Should().Be(new KpiPeriod(new LocalDate(2024, 10, 1), new LocalDate(2024, 12, 31)));
        created.ExpressedAs.Should().Be(KpiExpressedAs.ChangeFromBaseline);
        created.Status.Should().Be(KpiStatus.Active);
        created.Measurements.Select(m => m.Value).Should().BeEquivalentTo([12d, 13d]);
        created.Measurements.Should().AllSatisfy(m =>
        {
            m.MeasuredById.Should().BeNull();
            m.MeasuredOn.Should().Be(m.PeriodEnd);
            m.SampleSize.Should().Be(20);
        });
    }

    [Fact]
    public async Task CreateKpis_ImportsACumulativeKpi_WorkingOutEachPeriodFromItsAsOfDay()
    {
        // Arrange
        var anchor = new LocalDate(2026, 2, 16);
        var row = Row(1, Dto(measurements:
        [
            new(9.2, null, null, null, null, null, AsOf: new LocalDate(2026, 2, 27)),
            new(9.6, null, null, null, null, null, AsOf: new LocalDate(2026, 2, 20)),
        ]) with { Window = KpiMeasurementWindow.Cumulative(anchor) });

        // Act
        var result = await RunPass(row);

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeFalse(outcome.Error);
        var created = _harness.DbContext.Kpis.Single(k => k.Id == outcome.CreatedEntityId);
        created.Window.Should().Be(KpiMeasurementWindow.Cumulative(anchor));
        created.Measurements.Select(m => m.Period).Should().BeEquivalentTo(
        [
            new KpiPeriod(anchor, new LocalDate(2026, 2, 20)),
            new KpiPeriod(anchor, new LocalDate(2026, 2, 27)),
        ]);
        created.Measurements.Should().AllSatisfy(m => m.MeasuredOn.Should().Be(m.PeriodEnd));
    }

    [Fact]
    public async Task CreateKpis_ImportsSeveralPointInTimeReadingsOnOneDay_OnTheirDayInTheSystemTimeZone()
    {
        // Arrange
        var morning = Instant.FromUtc(2026, 3, 6, 15, 0);
        var lateEvening = Instant.FromUtc(2026, 3, 7, 3, 0);
        var row = Row(1, Dto(measurements:
        [
            new(45, null, null, null, null, null, MeasuredAt: morning),
            new(42, null, null, null, null, null, MeasuredAt: lateEvening),
        ]) with { Window = KpiMeasurementWindow.PointInTime });

        // Act
        var result = await RunPass(row);

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeFalse(outcome.Error);
        var created = _harness.DbContext.Kpis.Single(k => k.Id == outcome.CreatedEntityId);
        var march6 = new LocalDate(2026, 3, 6);
        created.Measurements.Should().HaveCount(2).And.AllSatisfy(m =>
        {
            m.Period.Should().Be(new KpiPeriod(march6, march6));
            m.MeasuredOn.Should().Be(march6);
        });
        created.LatestMeasurement()!.Value.Should().Be(42);
    }

    [Fact]
    public async Task CreateKpis_RejectsTheKpi_WhenAMeasurementIsNotGivenTheWayItsWindowTakes()
    {
        // Arrange
        var row = Row(1, Dto(measurements: [Month(0, 12)]) with { Window = KpiMeasurementWindow.Rolling(90, KpiRollingUnit.Days) });

        // Act
        var result = await RunPass(row);

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain(KpiErrors.AsOfDateRequired);
        _harness.DbContext.Kpis.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateKpis_ImportsARollingAverage()
    {
        // Arrange
        var row = Row(1, Dto(measurements: [Month(0, 12)]) with { RollingAverageOf = 7 });

        // Act
        var result = await RunPass(row);

        // Assert
        var created = _harness.DbContext.Kpis.Single(k => k.Id == result.Value.Rows.Single().CreatedEntityId);
        created.RollingAverageOf.Should().Be(7);
    }

    [Fact]
    public async Task CreateKpis_RejectsANameTheRecordAlreadyHas_SoAResubmittedFileAddsNothingTwice()
    {
        // Arrange
        _harness.AddKpi(name: "Throughput");
        var row = Row(1, Dto(name: " throughput ", measurements: [Month(0, 12)]));

        // Act
        var result = await RunPass(row);

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("already exists");
        _harness.DbContext.Kpis.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateKpis_RejectsTheSecondRowOfANameRepeatedForOneRecord()
    {
        // Arrange
        var first = Row(1, Dto(name: "Lead time"));
        var repeated = Row(2, Dto(name: "Lead Time"));

        // Act
        var result = await RunPass(first, repeated);

        // Assert
        var outcomes = result.Value.Rows;
        outcomes[0].Failed.Should().BeFalse(outcomes[0].Error);
        outcomes[1].Failed.Should().BeTrue();
        _harness.DbContext.Kpis.Select(k => k.Name).Should().Equal("Lead time");
    }

    [Fact]
    public async Task CreateKpis_ImportsAClosedKpi_WithItsHistoryKept()
    {
        // Arrange
        var row = Row(1, Dto(isClosed: true, measurements: [Month(0, 12)]));

        // Act
        var result = await RunPass(row);

        // Assert
        var created = _harness.DbContext.Kpis.Single(k => k.Id == result.Value.Rows.Single().CreatedEntityId);
        created.Status.Should().Be(KpiStatus.Closed);
        created.Measurements.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateKpis_RejectsAKpiWhoseHistoryOverlaps_AndKeepsTheOtherRows()
    {
        // Arrange
        var overlapping = new ImportKpiMeasurementDto(14, Jan1.PlusDays(10), Jan1.PlusDays(40), null, null, null);
        var bad = Row(1, Dto("Lead time", measurements: [Month(0, 12), overlapping]));
        var good = Row(2, Dto("Throughput"));

        // Act
        var result = await RunPass(bad, good);

        // Assert
        var outcomes = result.Value.Rows;
        outcomes[0].Failed.Should().BeTrue();
        outcomes[0].Error.Should().Contain(KpiErrors.PeriodOverlaps).And.Contain("2025-01-11 to 2025-02-10");
        outcomes[1].Failed.Should().BeFalse(outcomes[1].Error);
        _harness.DbContext.Kpis.Select(k => k.Name).Should().Equal("Throughput");
    }

    [Fact]
    public async Task CreateKpis_RejectsARowNamingARecordThatDoesNotExist()
    {
        // Arrange
        var row = Row(1, Dto(subjectId: Guid.NewGuid()));

        // Act
        var result = await RunPass(row);

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().StartWith("No gadget was found");
        _harness.DbContext.Kpis.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateKpis_RejectsAnInactiveCategory()
    {
        // Arrange
        var category = KpiCategory.Create("Flow", null, 0, EventActor.System, _harness.DateTimeProvider.Now).Value;
        category.Deactivate(EventActor.System, _harness.DateTimeProvider.Now);
        _harness.DbContext.AddCategories(category);
        var row = Row(1, Dto(categoryId: category.Id));

        // Act
        var result = await RunPass(row);

        // Assert
        result.Value.Rows.Single().Error.Should().Be($"No active KPI category was found with id '{category.Id}'.");
        _harness.DbContext.Kpis.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateKpis_RejectsAGoalTheKpiCannotHold()
    {
        // Arrange
        var dto = Dto() with { Baseline = KpiBaseline.None };

        // Act
        var result = await RunPass(Row(1, dto));

        // Assert
        result.Value.Rows.Single().Error.Should().Contain(KpiErrors.BaselineRequired);
        _harness.DbContext.Kpis.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateKpis_NumbersTwoNewKpisOnOneRecordInFileOrder()
    {
        // Arrange
        var first = Row(1, Dto("First"));
        var second = Row(2, Dto("Second"));

        // Act
        await RunPass(first, second);

        // Assert
        _harness.DbContext.Kpis.AsEnumerable().OrderBy(k => k.Order).Select(k => (k.Name, k.Order))
            .Should().Equal(("First", 0), ("Second", 1));
    }

    [Fact]
    public async Task DescribeCreatedRecords_LinksEachKpiToItsRecord()
    {
        // Arrange
        var kpi = _harness.AddKpi();

        // Act
        var links = await _definition.DescribeCreatedRecords([kpi.Id], TestContext.Current.CancellationToken);

        // Assert
        links.Should().ContainKey(kpi.Id).WhoseValue.Should().Be(new ImportRecordLink(ImportRecordType.Team, "14", "Cards"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task IsEnabled_FollowsTheKpisFlag(bool flag)
    {
        // Arrange
        _featureFlags.Setup(f => f.IsEnabled("kpis", It.IsAny<CancellationToken>())).ReturnsAsync(flag);

        // Act
        var enabled = await _definition.IsEnabled(TestContext.Current.CancellationToken);

        // Assert
        enabled.Should().Be(flag);
    }
}
