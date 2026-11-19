using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Kpis;
using Wayd.Common.Application.Kpis.Queries;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Kpis;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Kpis;

/// <summary>
/// The scorecard query against real SQL Server: the recent values are read per KPI, newest first and capped, inside one
/// projection, which in-memory fakes run as plain LINQ and so cannot show translates.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class GetKpiScorecardQueryIntegrationTests(SqlServerDbContextFixture fixture)
{
    private const string GadgetType = "test.kpi-scorecard.gadget";
    private static readonly KpiSubjectTypeDescriptor Gadget = new(GadgetType, "Gadget", "Gadget");
    private static readonly Instant Timestamp = Instant.FromUtc(2026, 11, 16, 9, 30);

    static GetKpiScorecardQueryIntegrationTests()
    {
        KpiSubjectTypes.Register(Gadget);
    }

    [Fact]
    public async Task Handle_ShouldReadEachKpisNewestValues_AndTheLatestReading()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var subjectId = Guid.CreateVersion7();
        await using (var seed = fixture.CreateContext())
        {
            var category = KpiCategory.Create($"Flow {Guid.NewGuid():N}"[..20], null, 0, EventActor.System, Timestamp).Value;
            seed.KpiCategories.Add(category);

            var goal = new KpiGoal(KpiTargetDirection.Increase, new KpiBaseline(10, null), KpiTarget.Absolute(20), null);
            var monthly = Kpi.Create(GadgetType, subjectId, new KpiDefinition("Monthly", null, category.Id, null, null, KpiExpressedAs.Value, goal), 0, EventActor.System, Timestamp).Value;
            for (var month = 0; month < 15; month++)
            {
                var start = new LocalDate(2025, 1, 1).PlusMonths(month);
                monthly.AddMeasurement(new KpiMeasurementInput(11 + month * 0.5, KpiMeasurementTime.ForPeriod(new KpiPeriod(start, start.PlusMonths(1).PlusDays(-1))),
                    null, null, start, null, KpiMeasurementSource.Manual, null, null), DateTimeZone.Utc, EventActor.System, Timestamp);
            }

            var empty = Kpi.Create(GadgetType, subjectId, new KpiDefinition("Unmeasured", null, null, null, null, KpiExpressedAs.Value, goal), 1, EventActor.System, Timestamp).Value;

            // Three readings on one day: only the moment orders them, so the query must order by it in SQL.
            var openBugs = Kpi.Create(GadgetType, subjectId,
                new KpiDefinition("Open bugs", null, null, null, null, KpiExpressedAs.Value, goal, KpiMeasurementWindow.PointInTime), 2, EventActor.System, Timestamp).Value;
            foreach (var (hour, value) in new[] { (17, 14d), (9, 12d), (13, 13d) })
            {
                var at = Instant.FromUtc(2026, 3, 6, hour, 0);
                openBugs.AddMeasurement(new KpiMeasurementInput(value, KpiMeasurementTime.AtMoment(at), null, null, new LocalDate(2026, 3, 6), null,
                    KpiMeasurementSource.Manual, null, null), DateTimeZone.Utc, EventActor.System, Timestamp);
            }

            seed.Kpis.AddRange(monthly, empty, openBugs);
            await seed.SaveChangesAsync(cancellationToken);
        }

        var subjectType = new Mock<IKpiSubjectType>();
        subjectType.Setup(t => t.SubjectType).Returns(GadgetType);
        subjectType.Setup(t => t.Resolve(subjectId, It.IsAny<CancellationToken>())).ReturnsAsync(new KpiSubjectAccess("Gadget One", null, false));
        var resolver = new KpiSubjectAccessResolver([subjectType.Object], new Mock<ICurrentPrincipal>().Object);

        await using var context = fixture.CreateContext();
        var handler = new GetKpiScorecardQueryHandler(resolver, context);

        // Act
        var result = await handler.Handle(new GetKpiScorecardQuery(GadgetType, subjectId), cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var kpis = result.Value!.Kpis;
        kpis.Select(k => k.Name).Should().Equal("Monthly", "Unmeasured", "Open bugs");
        kpis[2].RecentValues.Select(p => p.Value).Should().Equal(12, 13, 14);
        kpis[2].Reading.Value.Should().Be(14);
        kpis[2].Reading.Trend.Should().Be(KpiTrend.Improving);
        kpis[0].RecentValues.Should().HaveCount(12);
        kpis[0].RecentValues[0].PeriodStart.Should().Be(new LocalDate(2025, 4, 1));
        kpis[0].RecentValues[^1].Value.Should().Be(18);
        kpis[0].Reading.Value.Should().Be(18);
        kpis[0].Reading.Trend.Should().Be(KpiTrend.Improving);
        kpis[1].RecentValues.Should().BeEmpty();
        kpis[1].Reading.Value.Should().BeNull();
    }
}
