using FluentAssertions;
using NodaTime;
using NodaTime.Extensions;
using NodaTime.Testing;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Sut.Models;

public sealed class StrategicInitiativeKpiMeasurementTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly StrategicInitiativeKpiMeasurementFaker _kpiMeasurementFaker;

    public StrategicInitiativeKpiMeasurementTests()
    {
        _dateTimeProvider = new TestingDateTimeProvider(new FakeClock(DateTime.UtcNow.ToInstant()));
        _kpiMeasurementFaker = new StrategicInitiativeKpiMeasurementFaker(_dateTimeProvider);
    }

    [Fact]
    public void Create_ShouldReturnSuccess_WhenValidData()
    {
        // Arrange
        var measurement = _kpiMeasurementFaker.Generate();
        var timestamp = _dateTimeProvider.Now;

        // Act
        var result = StrategicInitiativeKpiMeasurement.Create(measurement.KpiId, measurement.ActualValue, measurement.MeasurementDate, measurement.MeasuredById, measurement.Note, timestamp);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.KpiId.Should().Be(measurement.KpiId);
        result.Value.ActualValue.Should().Be(measurement.ActualValue);
        result.Value.MeasurementDate.Should().Be(measurement.MeasurementDate);
        result.Value.MeasuredById.Should().Be(measurement.MeasuredById);
        result.Value.Note.Should().Be(measurement.Note);
    }

    [Fact]
    public void Create_ShouldReturnFailure_WhenMeasurementDateIsInTheFuture()
    {
        // Arrange
        var measurement = _kpiMeasurementFaker
            .WithMeasurementDate(_dateTimeProvider.Now.Plus(Duration.FromDays(1)))
            .Generate();
        var timestamp = _dateTimeProvider.Now;

        // Act
        var result = StrategicInitiativeKpiMeasurement.Create(measurement.KpiId, measurement.ActualValue, measurement.MeasurementDate, measurement.MeasuredById, measurement.Note, timestamp);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Measurement date cannot be in the future.");
    }
}