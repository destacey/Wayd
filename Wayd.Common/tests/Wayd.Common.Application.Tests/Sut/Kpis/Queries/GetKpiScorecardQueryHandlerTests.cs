using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Kpis.Queries;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Tests.Sut.Kpis.Queries;

public sealed class GetKpiScorecardQueryHandlerTests
{
    private readonly KpiTestHarness _harness = new();

    private GetKpiScorecardQueryHandler CreateHandler() => new(_harness.Resolver(), _harness.DbContext);

    private GetKpiScorecardQuery Query() => new(KpiTestHarness.Gadget.Key, _harness.SubjectId);

    [Fact]
    public async Task Handle_ShouldListActiveKpisByCategoryOrderThenPosition_UncategorizedLast()
    {
        // Arrange
        var now = _harness.DateTimeProvider.Now;
        var flow = KpiCategory.Create("Flow", null, 1, EventActor.System, now).Value;
        var quality = KpiCategory.Create("Quality", null, 0, EventActor.System, now).Value;
        _harness.DbContext.AddCategories(flow, quality);
        _harness.AddKpi("Loose", order: 0);
        _harness.AddKpi("Lead time", order: 2, categoryId: flow.Id);
        _harness.AddKpi("Throughput", order: 1, categoryId: flow.Id);
        _harness.AddKpi("Escaped bugs", order: 3, categoryId: quality.Id);
        _harness.AddKpi("Retired", order: 4).Close(EventActor.System, now);
        _harness.AddKpi("Elsewhere", subjectId: Guid.NewGuid());

        // Act
        var result = await CreateHandler().Handle(Query(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var scorecard = result.Value!;
        scorecard.SubjectName.Should().Be("Gadget One");
        scorecard.CanManage.Should().BeTrue();
        scorecard.Kpis.Select(k => k.Name).Should().Equal("Escaped bugs", "Throughput", "Lead time", "Loose");
        scorecard.Kpis[1].Category!.Name.Should().Be("Flow");
        scorecard.Kpis[3].Category.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReadTheLatestValue_AndCarryRecentValuesOldestFirst()
    {
        // Arrange
        var kpi = _harness.AddKpi();
        var now = _harness.DateTimeProvider.Now;
        for (var month = 1; month <= 14; month++)
        {
            var start = new LocalDate(2025, 1, 1).PlusMonths(month - 1);
            kpi.AddMeasurement(KpiTestHarness.Measurement(10 + month * 0.5, start, start.PlusMonths(1).PlusDays(-1)), EventActor.System, now);
        }

        // Act
        var result = await CreateHandler().Handle(Query(), TestContext.Current.CancellationToken);

        // Assert
        var summary = result.Value!.Kpis.Should().ContainSingle().Subject;
        summary.RecentValues.Should().HaveCount(12);
        summary.RecentValues[0].Value.Should().Be(11.5);
        summary.RecentValues[^1].Value.Should().Be(17);
        summary.Reading.Value.Should().Be(17);
        summary.Reading.Health.Should().Be(KpiHealth.AtRisk);
        summary.Reading.Trend.Should().Be(KpiTrend.Improving);
        summary.Reading.ProgressToTarget.Should().BeApproximately(0.7, 1e-9);
        summary.Goal.ResolvedAtRiskValue.Should().Be(16);
    }

    [Fact]
    public async Task Handle_ShouldReadNoValue_ForAKpiNotYetMeasured()
    {
        // Arrange
        _harness.AddKpi();

        // Act
        var result = await CreateHandler().Handle(Query(), TestContext.Current.CancellationToken);

        // Assert
        var summary = result.Value!.Kpis.Should().ContainSingle().Subject;
        summary.RecentValues.Should().BeEmpty();
        summary.Reading.Value.Should().BeNull();
        summary.Reading.Health.Should().BeNull();
        summary.Reading.Trend.Should().Be(KpiTrend.NoData);
    }

    [Fact]
    public async Task Handle_ShouldBeNull_WhenTheRecordIsHidden()
    {
        // Arrange
        _harness.AddKpi();
        _harness.SubjectHidden();

        // Act
        var result = await CreateHandler().Handle(Query(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }
}