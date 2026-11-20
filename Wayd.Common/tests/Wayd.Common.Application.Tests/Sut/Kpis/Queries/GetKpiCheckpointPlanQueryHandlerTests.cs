using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Kpis.Queries;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Tests.Sut.Kpis.Queries;

public sealed class GetKpiCheckpointPlanQueryHandlerTests
{
    private readonly KpiTestHarness _harness = new();

    private GetKpiCheckpointPlanQueryHandler CreateHandler() => new(_harness.Resolver(), _harness.DbContext);

    [Fact]
    public async Task Handle_ShouldReadEachCheckpoint_AgainstTheLatestMeasurementUnderIt()
    {
        // Arrange
        var kpi = _harness.AddKpi();
        var now = _harness.DateTimeProvider.Now;
        kpi.ChangeCheckpointPlan(
            [
                new KpiCheckpointValues(new LocalDate(2026, 4, 30), "Apr", 15, null),
                new KpiCheckpointValues(new LocalDate(2026, 2, 28), "Feb", 12, 11),
            ],
            EventActor.System, now);
        kpi.AddMeasurement(KpiTestHarness.Measurement(11, new LocalDate(2026, 2, 1), new LocalDate(2026, 2, 28)), KpiTestHarness.Zone, EventActor.System, now);
        kpi.AddMeasurement(KpiTestHarness.Measurement(15, new LocalDate(2026, 3, 1), new LocalDate(2026, 3, 1)), KpiTestHarness.Zone, EventActor.System, now);

        // Act
        var result = await CreateHandler().Handle(new GetKpiCheckpointPlanQuery(KpiTestHarness.Gadget.Key, _harness.SubjectId, kpi.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var readings = result.Value!;
        readings.Select(r => r.Checkpoint.Label).Should().Equal("Feb", "Apr");
        readings[0].Measurement!.Value.Should().Be(11);
        readings[0].Reading.Health.Should().Be(KpiHealth.AtRisk);
        readings[1].Measurement!.Value.Should().Be(15);
        readings[1].Reading.Health.Should().Be(KpiHealth.Healthy);
    }

    [Fact]
    public async Task Handle_ShouldBeEmpty_ForAKpiWithoutAPlan()
    {
        // Arrange
        var kpi = _harness.AddKpi();

        // Act
        var result = await CreateHandler().Handle(new GetKpiCheckpointPlanQuery(KpiTestHarness.Gadget.Key, _harness.SubjectId, kpi.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldBeNull_ForAKpiOnAnotherRecord()
    {
        // Arrange
        var elsewhere = _harness.AddKpi(subjectId: Guid.NewGuid());

        // Act
        var result = await CreateHandler().Handle(new GetKpiCheckpointPlanQuery(KpiTestHarness.Gadget.Key, _harness.SubjectId, elsewhere.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }
}
