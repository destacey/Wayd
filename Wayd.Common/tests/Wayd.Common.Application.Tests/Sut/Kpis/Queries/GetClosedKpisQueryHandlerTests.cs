using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Kpis.Queries;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Tests.Sut.Kpis.Queries;

public sealed class GetClosedKpisQueryHandlerTests
{
    private readonly KpiTestHarness _harness = new();

    private GetClosedKpisQueryHandler CreateHandler() => new(_harness.Resolver(), _harness.DbContext);

    private GetClosedKpisQuery Query() => new(KpiTestHarness.Gadget.Key, _harness.SubjectId);

    [Fact]
    public async Task Handle_ShouldListOnlyClosedKpis()
    {
        // Arrange
        var now = _harness.DateTimeProvider.Now;
        _harness.AddKpi("Open");
        _harness.AddKpi("Retired").Close(EventActor.System, now);
        _harness.AddKpi("Elsewhere", subjectId: Guid.NewGuid()).Close(EventActor.System, now);

        // Act
        var result = await CreateHandler().Handle(Query(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value!.Select(k => k.Name).Should().Equal("Retired");
    }

    [Fact]
    public async Task Handle_ShouldKeepAClosedKpisPlan_AndJudgeItsLatestValueAgainstIt()
    {
        // Arrange
        var now = _harness.DateTimeProvider.Now;
        var kpi = _harness.AddKpi();
        kpi.ChangeCheckpointPlan([new KpiCheckpointValues(new LocalDate(2026, 3, 31), "Q1", 12, 11)], EventActor.System, now);
        kpi.AddMeasurement(KpiTestHarness.Measurement(12, new LocalDate(2026, 3, 1), new LocalDate(2026, 3, 31)), KpiTestHarness.Zone, EventActor.System, now);
        kpi.Close(EventActor.System, now);

        // Act
        var result = await CreateHandler().Handle(Query(), TestContext.Current.CancellationToken);

        // Assert
        var summary = result.Value!.Should().ContainSingle().Subject;
        summary.Checkpoints.Should().ContainSingle().Which.Label.Should().Be("Q1");
        summary.Reading.Checkpoint!.Label.Should().Be("Q1");
        summary.Reading.Health.Should().Be(KpiHealth.Healthy);
    }
}
