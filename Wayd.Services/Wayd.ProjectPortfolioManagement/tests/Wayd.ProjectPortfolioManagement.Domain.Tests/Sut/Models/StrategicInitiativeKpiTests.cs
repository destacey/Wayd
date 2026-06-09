using FluentAssertions;
using NodaTime;
using NodaTime.Extensions;
using NodaTime.Testing;
using Wayd.Common.Domain.Models.KeyPerformanceIndicators;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data.Extensions;
using Wayd.Tests.Shared;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Sut.Models;

public sealed class StrategicInitiativeKpiTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly StrategicInitiativeKpiFaker _kpiFaker;
    private readonly StrategicInitiativeKpiCheckpointFaker _checkpointFaker;
    private readonly StrategicInitiativeKpiMeasurementFaker _measurementFaker;

    public StrategicInitiativeKpiTests()
    {
        _dateTimeProvider = new TestingDateTimeProvider(new FakeClock(DateTime.UtcNow.ToInstant()));
        _kpiFaker = new StrategicInitiativeKpiFaker();
        _checkpointFaker = new StrategicInitiativeKpiCheckpointFaker(_dateTimeProvider);
        _measurementFaker = new StrategicInitiativeKpiMeasurementFaker(_dateTimeProvider);
    }

    [Fact]
    public void Create_ShouldReturnSuccess_WhenValidData()
    {
        // Arrange
        var expectedKpi = _kpiFaker.Generate();
        var kpiParameters = expectedKpi.ToUpsertParameters();

        // Act
        var kpi = StrategicInitiativeKpi.Create(expectedKpi.StrategicInitiativeId, kpiParameters, order: 1);

        // Assert
        kpi.Should().NotBeNull();
        kpi.Name.Should().Be(expectedKpi.Name);
        kpi.Description.Should().Be(expectedKpi.Description);
        kpi.TargetValue.Should().Be(expectedKpi.TargetValue);
        kpi.ActualValue.Should().BeNull();
        kpi.Prefix.Should().Be(expectedKpi.Prefix);
        kpi.Suffix.Should().Be(expectedKpi.Suffix);
        kpi.TargetDirection.Should().Be(expectedKpi.TargetDirection);
        kpi.StrategicInitiativeId.Should().Be(expectedKpi.StrategicInitiativeId);
        kpi.Order.Should().Be(1);
    }

    [Fact]
    public void Update_ShouldUpdateProperties_WhenValidData()
    {
        // Arrange
        var kpi = _kpiFaker.Generate();
        var kpiParameters = kpi.ToUpsertParameters() with
        {
            Name = "Updated KPI",
            Description = "Updated description",
            TargetValue = 85.0,
            Prefix = "$",
            Suffix = "M",
            TargetDirection = kpi.TargetDirection is KpiTargetDirection.Increase ? KpiTargetDirection.Decrease : KpiTargetDirection.Increase
        };

        var expectedStrategicInitiativeId = kpi.StrategicInitiativeId;

        // Act
        var updateResult = kpi.Update(kpiParameters);

        // Assert
        updateResult.IsSuccess.Should().BeTrue();
        kpi.Name.Should().Be(kpiParameters.Name);
        kpi.Description.Should().Be(kpiParameters.Description);
        kpi.TargetValue.Should().Be(kpiParameters.TargetValue);
        kpi.Prefix.Should().Be(kpiParameters.Prefix);
        kpi.Suffix.Should().Be(kpiParameters.Suffix);
        kpi.TargetDirection.Should().Be(kpiParameters.TargetDirection);
        kpi.StrategicInitiativeId.Should().Be(expectedStrategicInitiativeId);
    }

    [Fact]
    public void CurrentValue_ShouldReturnLatestMeasurementValue()
    {
        // Arrange
        var kpi = _kpiFaker.Generate();
        var measurement1 = _measurementFaker.WithKpiId(kpi.Id).WithMeasurementDate(_dateTimeProvider.Now.Minus(Duration.FromDays(10))).Generate();
        var measurement2 = _measurementFaker.WithKpiId(kpi.Id).WithMeasurementDate(_dateTimeProvider.Now.Minus(Duration.FromDays(5))).Generate();

        // Act
        kpi.AddMeasurement(measurement1);
        kpi.AddMeasurement(measurement2);

        // Assert
        kpi.ActualValue.Should().Be(measurement2.ActualValue);
    }

    #region Checkpoints

    [Fact]
    public void AddCheckpoint_ShouldAddCheckpoint_WhenValidData()
    {
        // Arrange
        var kpi = _kpiFaker.Generate();
        var checkpoint = _checkpointFaker.WithKpiId(kpi.Id).Generate();

        // Act
        var addResult = kpi.AddCheckpoint(checkpoint);

        // Assert
        addResult.IsSuccess.Should().BeTrue();
        kpi.Checkpoints.Should().Contain(checkpoint);
    }

    [Fact]
    public void AddCheckpoint_ShouldReturnFailure_WhenKpiIdMismatch()
    {
        // Arrange
        var kpi = _kpiFaker.Generate();
        var checkpoint = _checkpointFaker.Generate();

        // Act
        var addResult = kpi.AddCheckpoint(checkpoint);

        // Assert
        addResult.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AddCheckpoint_ShouldReturnFailure_WhenCheckpointDateIsDuplicate()
    {
        // Arrange
        var kpi = _kpiFaker.Generate();
        var checkpointDate = _dateTimeProvider.Now.Minus(Duration.FromDays(1));
        var checkpoint1 = _checkpointFaker.WithKpiId(kpi.Id).WithCheckpointDate(checkpointDate).Generate();
        var checkpoint2 = _checkpointFaker.WithKpiId(kpi.Id).WithCheckpointDate(checkpointDate).Generate();
        kpi.AddCheckpoint(checkpoint1);

        // Act
        var addResult = kpi.AddCheckpoint(checkpoint2);

        // Assert
        addResult.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RemoveCheckpoint_ShouldRemoveCheckpoint_WhenValidId()
    {
        // Arrange
        var kpi = _kpiFaker.Generate();
        var checkpoint = _checkpointFaker.WithKpiId(kpi.Id).Generate();
        kpi.AddCheckpoint(checkpoint);

        // Act
        var removeResult = kpi.RemoveCheckpoint(checkpoint.Id);

        // Assert
        removeResult.IsSuccess.Should().BeTrue();
        kpi.Checkpoints.Should().NotContain(checkpoint);
    }

    [Fact]
    public void RemoveCheckpoint_ShouldReturnFailure_WhenInvalidId()
    {
        // Arrange
        var kpi = _kpiFaker.Generate();
        var invalidCheckpointId = Guid.NewGuid();

        // Act
        var removeResult = kpi.RemoveCheckpoint(invalidCheckpointId);

        // Assert
        removeResult.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ManageCheckpointPlan_ShouldAddNewCheckpoints_WhenNoIdProvided()
    {
        // Arrange
        var kpi = _kpiFaker.Generate();
        var upsert = UpsertStrategicInitiativeKpiCheckpoint.Create(null, 10.0, _dateTimeProvider.Now, "Q1");

        // Act
        var result = kpi.ManageCheckpointPlan([upsert]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        kpi.Checkpoints.Should().HaveCount(1);
        var checkpoint = kpi.Checkpoints.Single();
        checkpoint.KpiId.Should().Be(kpi.Id);
        checkpoint.TargetValue.Should().Be(upsert.TargetValue);
        checkpoint.CheckpointDate.Should().Be(upsert.CheckpointDate);
        checkpoint.DateLabel.Should().Be(upsert.DateLabel);
    }

    [Fact]
    public void ManageCheckpointPlan_ShouldUpdateExistingCheckpoint_WhenIdProvided()
    {
        // Arrange
        var kpi = _kpiFaker.Generate();
        var existingCheckpoint = _checkpointFaker.WithKpiId(kpi.Id).Generate();
        kpi.AddCheckpoint(existingCheckpoint);

        var updatedTargetValue = existingCheckpoint.TargetValue + 1;
        var updatedCheckpointDate = existingCheckpoint.CheckpointDate.Plus(Duration.FromDays(1));
        const string updatedDateLabel = "Updated";

        var upsert = UpsertStrategicInitiativeKpiCheckpoint.Create(existingCheckpoint.Id, updatedTargetValue, updatedCheckpointDate, updatedDateLabel);

        // Act
        var result = kpi.ManageCheckpointPlan([upsert]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        kpi.Checkpoints.Should().HaveCount(1);
        var checkpoint = kpi.Checkpoints.Single();
        checkpoint.Id.Should().Be(existingCheckpoint.Id);
        checkpoint.TargetValue.Should().Be(updatedTargetValue);
        checkpoint.CheckpointDate.Should().Be(updatedCheckpointDate);
        checkpoint.DateLabel.Should().Be(updatedDateLabel);
    }

    [Fact]
    public void ManageCheckpointPlan_ShouldRemoveCheckpointsNotInRequest()
    {
        // Arrange
        var kpi = _kpiFaker.Generate();
        var checkpoint1 = _checkpointFaker.WithKpiId(kpi.Id).Generate();
        var checkpoint2 = _checkpointFaker.WithKpiId(kpi.Id).Generate();
        kpi.AddCheckpoint(checkpoint1);
        kpi.AddCheckpoint(checkpoint2);

        var upsert = UpsertStrategicInitiativeKpiCheckpoint.Create(checkpoint1.Id, checkpoint1.TargetValue, checkpoint1.CheckpointDate, checkpoint1.DateLabel);

        // Act
        var result = kpi.ManageCheckpointPlan([upsert]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        kpi.Checkpoints.Should().ContainSingle(c => c.Id == checkpoint1.Id);
        kpi.Checkpoints.Should().NotContain(c => c.Id == checkpoint2.Id);
    }

    [Fact]
    public void ManageCheckpointPlan_ShouldReturnFailure_WhenCheckpointNotFound()
    {
        // Arrange
        var kpi = _kpiFaker.Generate();
        var unknownCheckpointId = Guid.NewGuid();
        var upsert = UpsertStrategicInitiativeKpiCheckpoint.Create(unknownCheckpointId, 10.0, _dateTimeProvider.Now, "Q1");

        // Act
        var result = kpi.ManageCheckpointPlan([upsert]);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ManageCheckpointPlan_ShouldReturnFailure_WhenDuplicateIdsProvided()
    {
        // Arrange
        var kpi = _kpiFaker.Generate();
        var checkpoint = _checkpointFaker.WithKpiId(kpi.Id).Generate();
        kpi.AddCheckpoint(checkpoint);

        var upsert1 = UpsertStrategicInitiativeKpiCheckpoint.Create(checkpoint.Id, checkpoint.TargetValue, checkpoint.CheckpointDate, checkpoint.DateLabel);
        var upsert2 = UpsertStrategicInitiativeKpiCheckpoint.Create(checkpoint.Id, checkpoint.TargetValue + 1, checkpoint.CheckpointDate.Plus(Duration.FromDays(1)), "Dup");

        // Act
        var result = kpi.ManageCheckpointPlan([upsert1, upsert2]);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ManageCheckpointPlan_ShouldReturnFailure_WhenDuplicateCheckpointDatesProvided()
    {
        // Arrange
        var kpi = _kpiFaker.Generate();
        var checkpointDate = _dateTimeProvider.Now.Minus(Duration.FromDays(1));

        var upsert1 = UpsertStrategicInitiativeKpiCheckpoint.Create(null, 10.0, checkpointDate, "Q1");
        var upsert2 = UpsertStrategicInitiativeKpiCheckpoint.Create(null, 11.0, checkpointDate, "Q1-dup");

        // Act
        var result = kpi.ManageCheckpointPlan([upsert1, upsert2]);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ManageCheckpointPlan_ShouldReturnFailure_WhenUpdatingCheckpointToDuplicateDate()
    {
        // Arrange
        var kpi = _kpiFaker.Generate();
        var checkpoint1 = _checkpointFaker.WithKpiId(kpi.Id).WithCheckpointDate(_dateTimeProvider.Now.Minus(Duration.FromDays(2))).Generate();
        var checkpoint2 = _checkpointFaker.WithKpiId(kpi.Id).WithCheckpointDate(_dateTimeProvider.Now.Minus(Duration.FromDays(1))).Generate();
        kpi.AddCheckpoint(checkpoint1);
        kpi.AddCheckpoint(checkpoint2);

        // Attempt to update checkpoint2 to the same date as checkpoint1
        var upsert1 = UpsertStrategicInitiativeKpiCheckpoint.Create(checkpoint1.Id, checkpoint1.TargetValue, checkpoint1.CheckpointDate, checkpoint1.DateLabel);
        var upsert2 = UpsertStrategicInitiativeKpiCheckpoint.Create(checkpoint2.Id, checkpoint2.TargetValue, checkpoint1.CheckpointDate, checkpoint2.DateLabel);

        // Act
        var result = kpi.ManageCheckpointPlan([upsert1, upsert2]);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion Checkpoints

    #region Measurements

    [Fact]
    public void AddMeasurement_ShouldAddMeasurement_WhenValidData()
    {
        // Arrange
        var kpi = _kpiFaker.Generate();
        var measurement = _measurementFaker.WithKpiId(kpi.Id).Generate();

        // Act
        var addResult = kpi.AddMeasurement(measurement);

        // Assert
        addResult.IsSuccess.Should().BeTrue();
        kpi.Measurements.Should().Contain(measurement);
        kpi.ActualValue.Should().Be(measurement.ActualValue);
    }

    [Fact]
    public void AddMeasurement_ShouldReturnFailure_WhenKpiIdMismatch()
    {
        // Arrange
        var kpi = _kpiFaker.Generate();
        var measurement = _measurementFaker.Generate();

        // Act
        var addResult = kpi.AddMeasurement(measurement);

        // Assert
        addResult.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AddMeasurement_ShouldReturnFailure_WhenMeasurementDateIsDuplicate()
    {
        // Arrange
        var kpi = _kpiFaker.Generate();
        var measurementDate = _dateTimeProvider.Now.Minus(Duration.FromDays(1));
        var measurement1 = _measurementFaker.WithKpiId(kpi.Id).WithMeasurementDate(measurementDate).Generate();
        var measurement2 = _measurementFaker.WithKpiId(kpi.Id).WithMeasurementDate(measurementDate).Generate();
        kpi.AddMeasurement(measurement1);

        // Act
        var addResult = kpi.AddMeasurement(measurement2);

        // Assert
        addResult.IsFailure.Should().BeTrue();
        addResult.Error.Should().Be("Measurement dates must be unique within the KPI.");
        kpi.Measurements.Should().HaveCount(1);
    }

    [Fact]
    public void AddMeasurement_ShouldAllowMultipleMeasurements_WhenDatesAreDistinct()
    {
        // Arrange
        var kpi = _kpiFaker.Generate();
        var measurement1 = _measurementFaker.WithKpiId(kpi.Id).WithMeasurementDate(_dateTimeProvider.Now.Minus(Duration.FromDays(2))).Generate();
        var measurement2 = _measurementFaker.WithKpiId(kpi.Id).WithMeasurementDate(_dateTimeProvider.Now.Minus(Duration.FromDays(1))).Generate();

        // Act
        var result1 = kpi.AddMeasurement(measurement1);
        var result2 = kpi.AddMeasurement(measurement2);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        kpi.Measurements.Should().HaveCount(2);
    }

    [Fact]
    public void RemoveMeasurement_ShouldRemoveMeasurement_WhenValidId()
    {
        // Arrange
        var kpi = _kpiFaker.Generate();
        var measurement = _measurementFaker.WithKpiId(kpi.Id).Generate();
        kpi.AddMeasurement(measurement);

        // Act
        var removeResult = kpi.RemoveMeasurement(measurement.Id);

        // Assert
        removeResult.IsSuccess.Should().BeTrue();
        kpi.Measurements.Should().NotContain(measurement);
        kpi.ActualValue.Should().BeNull();
    }

    [Fact]
    public void RemoveMeasurement_ShouldReturnFailure_WhenInvalidId()
    {
        // Arrange
        var kpi = _kpiFaker.Generate();
        var invalidMeasurementId = Guid.NewGuid();

        // Act
        var removeResult = kpi.RemoveMeasurement(invalidMeasurementId);

        // Assert
        removeResult.IsFailure.Should().BeTrue();
    }

    #endregion Measurements
}