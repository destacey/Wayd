using System.Data;
using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Models.KeyPerformanceIndicators;

namespace Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;

public sealed class StrategicInitiativeKpi : Kpi
{
    private readonly HashSet<StrategicInitiativeKpiCheckpoint> _checkpoints = [];
    private readonly HashSet<StrategicInitiativeKpiMeasurement> _measurements = [];

    private StrategicInitiativeKpi() : base() { }

    private StrategicInitiativeKpi(string name, string? description, double? startingValue, double targetValue, string? prefix, string? suffix, KpiTargetDirection direction, Guid strategicInitiativeId, int order)
        : base(name, description, startingValue, targetValue, prefix, suffix, direction)
    {
        StrategicInitiativeId = strategicInitiativeId;
        Order = order;
    }

    /// <summary>
    /// The unique identifier of the associated Strategic Initiative.
    /// </summary>
    public Guid StrategicInitiativeId { get; private set; }

    /// <summary>
    /// The display order of the KPI within its strategic initiative. Lower values appear first.
    /// </summary>
    public int Order { get; internal set; }

    /// <summary>
    /// The collection of KPI checkpoints.
    /// </summary>
    public IReadOnlyCollection<StrategicInitiativeKpiCheckpoint> Checkpoints => _checkpoints;

    /// <summary>
    /// The collection of KPI measurements.
    /// </summary>
    public IReadOnlyCollection<StrategicInitiativeKpiMeasurement> Measurements => _measurements;

    /// <summary>
    /// Updates the KPI with the provided parameters.
    /// </summary>
    /// <param name="parameters"></param>
    /// <returns></returns>
    internal Result Update(StrategicInitiativeKpiUpsertParameters parameters)
    {
        Guard.Against.Null(parameters, nameof(parameters));

        return base.Update(parameters.Name, parameters.Description, parameters.StartingValue, parameters.TargetValue, parameters.Prefix, parameters.Suffix, parameters.TargetDirection);
    }

    /// <summary>
    /// Adds a new checkpoint entry to the KPI.
    /// </summary>
    /// <param name="checkpoint">The KPI checkpoint entry to add.</param>
    internal Result AddCheckpoint(StrategicInitiativeKpiCheckpoint checkpoint)
    {
        Guard.Against.Null(checkpoint, nameof(checkpoint));

        if (checkpoint.KpiId != Id)
            return Result.Failure("Checkpoint does not belong to this KPI.");

        if (_checkpoints.Any(c => c.CheckpointDate == checkpoint.CheckpointDate))
            return Result.Failure("Checkpoint dates must be unique within the KPI.");

        _checkpoints.Add(checkpoint);

        return Result.Success();
    }

    internal Result ManageCheckpointPlan(IEnumerable<UpsertStrategicInitiativeKpiCheckpoint> checkpoints)
    {
        Guard.Against.Null(checkpoints, nameof(checkpoints));

        var checkpointList = checkpoints.ToList();

        // verify no duplicate ids
        var checkpointIds = checkpointList.Where(c => !c.IsNew).Select(c => c.Id!.Value).ToList();
        if (checkpointIds.Distinct().Count() != checkpointIds.Count)
            return Result.Failure("Checkpoint IDs must be unique within the KPI.");

        // verify no duplicate checkpoint dates (this represents the final desired plan)
        var checkpointDates = checkpointList.Select(c => c.CheckpointDate).ToList();
        if (checkpointDates.Distinct().Count() != checkpointDates.Count)
            return Result.Failure("Checkpoint dates must be unique within the KPI.");

        // remove any checkpoints that are not in the list
        var removedCheckpoints = _checkpoints.Where(c => !checkpointList.Any(x => x.Id == c.Id)).ToList();
        foreach (var removedCheckpoint in removedCheckpoints)
        {
            var deleteResult = RemoveCheckpoint(removedCheckpoint.Id);
            if (deleteResult.IsFailure)
                return Result.Failure(deleteResult.Error);
        }

        // update existing checkpoints
        foreach (var checkpoint in checkpointList.Where(c => !c.IsNew))
        {
            var existingCheckpoint = _checkpoints.FirstOrDefault(c => c.Id == checkpoint.Id);
            if (existingCheckpoint is null)
                return Result.Failure($"Checkpoint {checkpoint.Id} not found.");

            var updateResult = existingCheckpoint.Update(checkpoint.TargetValue, checkpoint.CheckpointDate, checkpoint.DateLabel, checkpoint.AtRiskValue);
            if (updateResult.IsFailure)
                return Result.Failure(updateResult.Error);
        }

        // add new checkpoints
        foreach (var checkpoint in checkpointList.Where(c => c.IsNew))
        {
            var checkpointToAdd = StrategicInitiativeKpiCheckpoint.Create(Id, checkpoint.TargetValue, checkpoint.CheckpointDate, checkpoint.DateLabel, checkpoint.AtRiskValue);
            var addResult = AddCheckpoint(checkpointToAdd);
            if (addResult.IsFailure)
                return Result.Failure(addResult.Error);
        }

        return Result.Success();
    }

    /// <summary>
    /// Removes a checkpoint entry from the KPI.
    /// </summary>
    /// <param name="checkpointId"></param>
    /// <returns></returns>
    internal Result RemoveCheckpoint(Guid checkpointId)
    {
        Guard.Against.NullOrEmpty(checkpointId, nameof(checkpointId));

        var checkpoint = _checkpoints.FirstOrDefault(m => m.Id == checkpointId);
        if (checkpoint == null)
            return Result.Failure("Checkpoint not found in the KPI.");

        _checkpoints.Remove(checkpoint);

        return Result.Success();
    }

    /// <summary>
    /// Adds a new measurement entry to the KPI.
    /// </summary>
    /// <param name="measurement">The KPI measurement entry to add.</param>
    internal Result AddMeasurement(StrategicInitiativeKpiMeasurement measurement)
    {
        Guard.Against.Null(measurement, nameof(measurement));

        if (measurement.KpiId != Id)
            return Result.Failure("Measurement does not belong to this KPI.");

        if (_measurements.Any(m => m.MeasurementDate == measurement.MeasurementDate))
            return Result.Failure("Measurement dates must be unique within the KPI.");

        _measurements.Add(measurement);

        SetActualValueFromMeasurements();

        return Result.Success();
    }

    /// <summary>
    /// Removes a measurement entry from the KPI.
    /// </summary>
    /// <param name="measurementId"></param>
    /// <returns></returns>
    internal Result RemoveMeasurement(Guid measurementId)
    {
        Guard.Against.NullOrEmpty(measurementId, nameof(measurementId));

        var measurement = _measurements.FirstOrDefault(m => m.Id == measurementId);
        if (measurement == null)
            return Result.Failure("Measurement not found in the KPI.");

        _measurements.Remove(measurement);

        SetActualValueFromMeasurements();

        return Result.Success();
    }

    /// <summary>
    /// Sets the actual value of the KPI based on the latest measurement entry.
    /// </summary>
    private void SetActualValueFromMeasurements()
    {
        var latestMeasurement = _measurements
            .OrderByDescending(m => m.MeasurementDate)
            .FirstOrDefault();

        ActualValue = latestMeasurement?.ActualValue;
    }

    /// <summary>
    /// Factory method to create a new instance of <see cref="StrategicInitiativeKpi"/>.
    /// </summary>
    /// <param name="strategicInitiativeId">The unique identifier of the associated Strategic Initiative.</param>
    /// <param name="parameters">The KPI upsert parameters.</param>
    /// <param name="order">The display order of the KPI within the strategic initiative.</param>
    /// <returns></returns>
    internal static StrategicInitiativeKpi Create(Guid strategicInitiativeId, StrategicInitiativeKpiUpsertParameters parameters, int order)
    {
        return new StrategicInitiativeKpi(parameters.Name, parameters.Description, parameters.StartingValue, parameters.TargetValue, parameters.Prefix, parameters.Suffix, parameters.TargetDirection, strategicInitiativeId, order);
    }
}
