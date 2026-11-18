using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Kpis;
using Wayd.Common.Application.Kpis.Commands;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Events;

namespace Wayd.Common.Application.Tests.Sut.Kpis.Commands;

public sealed class ChangeKpiMeasurementCommandHandlerTests
{
    private static readonly LocalDate Jan1 = new(2026, 1, 1);
    private static readonly LocalDate Jan31 = new(2026, 1, 31);

    private readonly KpiTestHarness _harness = new();

    private ChangeKpiMeasurementCommandHandler CreateHandler() => new(_harness.Scope());

    [Fact]
    public async Task Handle_ShouldCorrectTheValue_AndKeepWhoTookIt()
    {
        // Arrange
        var kpi = _harness.AddKpi();
        var takenBy = Guid.NewGuid();
        var measurement = kpi.AddMeasurement(KpiTestHarness.Measurement(12, Jan1, Jan31, takenBy), KpiTestHarness.Zone, EventActor.System, _harness.DateTimeProvider.Now).Value;
        var command = new ChangeKpiMeasurementCommand(KpiTestHarness.Gadget.Key, _harness.SubjectId, kpi.Id, measurement.Id,
            new KpiMeasurementValuesInput(13, Jan1, Jan31, null, null, null, null, "Recounted"));

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        measurement.Value.Should().Be(13);
        measurement.Note.Should().Be("Recounted");
        measurement.MeasuredById.Should().Be(takenBy);
        _harness.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldBeNotFound_ForAMeasurementOnAnotherKpi()
    {
        // Arrange
        var kpi = _harness.AddKpi();
        var other = _harness.AddKpi("Other");
        var measurement = other.AddMeasurement(KpiTestHarness.Measurement(12, Jan1, Jan31), KpiTestHarness.Zone, EventActor.System, _harness.DateTimeProvider.Now).Value;
        var command = new ChangeKpiMeasurementCommand(KpiTestHarness.Gadget.Key, _harness.SubjectId, kpi.Id, measurement.Id,
            new KpiMeasurementValuesInput(13, Jan1, Jan31, null, null, null, null, null));

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(KpiAccessErrors.NotFound);
        measurement.Value.Should().Be(12);
        _harness.DbContext.SaveChangesCallCount.Should().Be(0);
    }
}