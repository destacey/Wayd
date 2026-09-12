using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Models;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.Domain.Tests.Data;
using Wayd.Tests.Shared;
using NodaTime.Extensions;
using NodaTime.Testing;

namespace Wayd.Planning.Domain.Tests.Sut.Models;

public class PlanningIntervalTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly PlanningIntervalFaker _planningIntervalFaker = new();

    public PlanningIntervalTests()
    {
        _dateTimeProvider = new(new FakeClock(DateTime.UtcNow.ToInstant()));
    }

    private void SetObjectiveStatus(PlanningInterval planningInterval, Guid objectiveId, ObjectiveStatus status, bool isStretch)
    {
        var objective = planningInterval.Objectives.Single(o => o.Id == objectiveId);
        planningInterval.UpdateObjective(objectiveId, objective.Name, objective.Description, status, objective.Progress, objective.StartDate, objective.TargetDate, isStretch, _dateTimeProvider.Now);
    }

    #region StateOn

    [Fact]
    public void StateOn_ShouldReturnCompleted_WhenDateIsPast()
    {
        // Arrange
        var today = _dateTimeProvider.Today;
        var planningIntervalDateRange = new LocalDateRange(today.Plus(Period.FromWeeks(-14)), today.Plus(Period.FromWeeks(-2)));
        var sut = _planningIntervalFaker.WithDateRange(planningIntervalDateRange).Generate();

        // Act
        var result = sut.StateOn(today);

        // Assert
        result.Should().Be(IterationState.Completed);
    }

    [Fact]
    public void StateOn_ShouldReturnActive_WhenDateIsWithinRange()
    {
        // Arrange
        var today = _dateTimeProvider.Today;
        var planningIntervalDateRange = new LocalDateRange(today.Plus(Period.FromWeeks(-1)), today.Plus(Period.FromWeeks(11)));
        var sut = _planningIntervalFaker.WithDateRange(planningIntervalDateRange).Generate();

        // Act
        var result = sut.StateOn(today);

        // Assert
        result.Should().Be(IterationState.Active);
    }

    [Fact]
    public void StateOn_ShouldReturnFuture_WhenDateIsFuture()
    {
        // Arrange
        var today = _dateTimeProvider.Today;
        var planningIntervalDateRange = new LocalDateRange(today.Plus(Period.FromWeeks(1)), today.Plus(Period.FromWeeks(13)));
        var sut = _planningIntervalFaker.WithDateRange(planningIntervalDateRange).Generate();

        // Act
        var result = sut.StateOn(today);

        // Assert
        result.Should().Be(IterationState.Future);
    }

    #endregion StateOn

    #region CalculatePredictability

    [Fact]
    public void CalculatePredictability_WhenStartedAndNoObjectives_ReturnsNull()
    {
        // Arrange
        var sut = _planningIntervalFaker.Generate();

        // Act
        var result = sut.CalculatePredictability(_dateTimeProvider.Today);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void CalculatePredictability_WhenFuture_ReturnsNull()
    {
        // Arrange
        var sut = _planningIntervalFaker.Generate();
        sut.Update(sut.Name, sut.Description, false);

        // Act
        var result = sut.CalculatePredictability(_dateTimeProvider.Today);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void CalculatePredictability_WhenNoCompletedObjectives_Returns0()
    {
        // Arrange
        var team = new PlanningTeamFaker(TeamType.Team).Generate();
        var sut = _planningIntervalFaker.WithObjectives(team, 2).Generate();

        // Act
        var result = sut.CalculatePredictability(_dateTimeProvider.Today);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void CalculatePredictability_WhenHalfOfObjectivesCompleted_Returns50()
    {
        // Arrange
        var team = new PlanningTeamFaker(TeamType.Team).Generate();
        var sut = _planningIntervalFaker.WithObjectives(team, 6).Generate();

        var objectiveIds = sut.Objectives.Select(o => o.Id).ToArray();
        for (int i = 0; i < 3; i++)
        {
            SetObjectiveStatus(sut, objectiveIds[i], ObjectiveStatus.Completed, false);
        }

        // Act
        var result = sut.CalculatePredictability(_dateTimeProvider.Today);

        // Assert
        result.Should().Be(50);
    }

    [Fact]
    public void CalculatePredictability_WithStretchAndWhenAllCompleted_Returns100()
    {
        // Arrange
        var objectiveCount = 6;
        var team = new PlanningTeamFaker(TeamType.Team).Generate();
        var sut = new PlanningIntervalFaker().WithObjectives(team, objectiveCount).Generate();

        var objectiveIds = sut.Objectives.Select(o => o.Id).ToArray();
        for (int i = 0; i < objectiveCount; i++)
        {
            var isStretch = i >= objectiveCount - 2;
            SetObjectiveStatus(sut, objectiveIds[i], ObjectiveStatus.Completed, isStretch);
        }

        // Act
        var result = sut.CalculatePredictability(_dateTimeProvider.Today);

        // Assert
        result.Should().Be(100);
    }

    [Fact]
    public void CalculatePredictability_WhenAllButOneNonStretchCompleted_Returns100()
    {
        // Arrange
        var objectiveCount = 6;
        var team = new PlanningTeamFaker(TeamType.Team).Generate();
        var sut = _planningIntervalFaker.WithObjectives(team, objectiveCount).Generate();

        var objectiveIds = sut.Objectives.Select(o => o.Id).ToArray();
        for (int i = 1; i < objectiveCount; i++) // skip the first one so it is still open
        {
            var isStretch = i >= objectiveCount - 2;
            SetObjectiveStatus(sut, objectiveIds[i], ObjectiveStatus.Completed, isStretch);
        }

        // Act
        var result = sut.CalculatePredictability(_dateTimeProvider.Today);

        // Assert
        result.Should().Be(100);
    }

    [Fact]
    public void CalculatePredictability_WhenAllCompletedExceptStretch_ReturnsOneHundred()
    {
        // Arrange
        var objectiveCount = 6;
        var team = new PlanningTeamFaker(TeamType.Team).Generate();
        var sut = _planningIntervalFaker.WithObjectives(team, objectiveCount).Generate();

        var objectiveIds = sut.Objectives.Select(o => o.Id).ToArray();
        for (int i = 0; i < objectiveCount; i++)
        {
            var isStretch = i >= objectiveCount - 2;
            var status = isStretch ? Enums.ObjectiveStatus.InProgress : Enums.ObjectiveStatus.Completed;
            SetObjectiveStatus(sut, objectiveIds[i], status, isStretch);
        }

        // Act
        var result = sut.CalculatePredictability(_dateTimeProvider.Today);

        // Assert
        result.Should().Be(100);
    }

    [Fact]
    public void CalculatePredictability_WhenThreeNonStretchComplete_Returns75()
    {
        // Arrange
        var objectiveCount = 6;
        var team = new PlanningTeamFaker(TeamType.Team).Generate();
        var sut = _planningIntervalFaker.WithObjectives(team, objectiveCount).Generate();

        var objectiveIds = sut.Objectives.Select(o => o.Id).ToArray();
        for (int i = 0; i < objectiveCount; i++)
        {
            var isStretch = i >= objectiveCount - 2;
            var isComplete = i < 3;
            var status = isComplete ? Enums.ObjectiveStatus.Completed : Enums.ObjectiveStatus.InProgress;
            SetObjectiveStatus(sut, objectiveIds[i], status, isStretch);
        }

        // Act
        var result = sut.CalculatePredictability(_dateTimeProvider.Today);

        // Assert
        // 3/4 of the non-stretch objectives are complete and 0/2 of the stretch objectives are complete, so 75% predictability
        result.Should().Be(75);
    }

    [Fact]
    public void CalculatePredictability_WhenOnlyStretchComplete_Returns50()
    {
        // Arrange
        var objectiveCount = 6;
        var team = new PlanningTeamFaker(TeamType.Team).Generate();
        var sut = _planningIntervalFaker.WithObjectives(team, objectiveCount).Generate();

        var objectiveIds = sut.Objectives.Select(o => o.Id).ToArray();
        for (int i = 0; i < objectiveCount; i++)
        {
            var isStretch = i >= objectiveCount - 2;
            var isComplete = i < 3;
            var status = isStretch ? Enums.ObjectiveStatus.Completed : Enums.ObjectiveStatus.InProgress;
            SetObjectiveStatus(sut, objectiveIds[i], status, isStretch);
        }

        // Act
        var result = sut.CalculatePredictability(_dateTimeProvider.Today);

        // Assert
        // 3/4 of the non-stretch objectives are complete and 0/2 of the stretch objectives are complete, so 75% predictability
        result.Should().Be(50);
    }

    #endregion CalculatePredictability

    #region Initialize Iterations

    [Fact]
    public void InitializeIterations_WhenNoIterationsExist_ReturnsSuccess()
    {
        // Arrange
        var sut = _planningIntervalFaker.Generate();
        sut.ManageDates(new LocalDateRange(new LocalDate(2023, 10, 2), new LocalDate(2023, 11, 26)), []);
        var expectedIterations = 4;

        // Act
        var result = sut.InitializeIterations(2, "Iteration ");
        var iterations = sut.Iterations.ToList();

        // Assert
        result.IsSuccess.Should().BeTrue();
        iterations.Count.Should().Be(expectedIterations);
        iterations[0].Name.Should().Be("Iteration 1");
        iterations[0].Category.Should().Be(IterationCategory.Development);
        iterations[0].DateRange.Start.Should().Be(new LocalDate(2023, 10, 2));
        iterations[0].DateRange.End.Should().Be(new LocalDate(2023, 10, 15));
        iterations[1].Name.Should().Be("Iteration 2");
        iterations[1].Category.Should().Be(IterationCategory.Development);
        iterations[1].DateRange.Start.Should().Be(new LocalDate(2023, 10, 16));
        iterations[1].DateRange.End.Should().Be(new LocalDate(2023, 10, 29));
        iterations[2].Name.Should().Be("Iteration 3");
        iterations[2].Category.Should().Be(IterationCategory.Development);
        iterations[2].DateRange.Start.Should().Be(new LocalDate(2023, 10, 30));
        iterations[2].DateRange.End.Should().Be(new LocalDate(2023, 11, 12));
        iterations[3].Name.Should().Be("Iteration 4");
        iterations[3].Category.Should().Be(IterationCategory.InnovationAndPlanning);
        iterations[3].DateRange.Start.Should().Be(new LocalDate(2023, 11, 13));
        iterations[3].DateRange.End.Should().Be(new LocalDate(2023, 11, 26));
    }

    [Fact]
    public void InitializeIterations_WhenNoIterationsExistAndUnevenDates_ReturnsSuccess()
    {
        // Arrange
        var sut = _planningIntervalFaker.Generate();
        sut.ManageDates(new LocalDateRange(new LocalDate(2023, 10, 2), new LocalDate(2023, 11, 27)), []);
        var expectedIterations = 5;

        // Act
        var result = sut.InitializeIterations(2, "Iteration ");
        var iterations = sut.Iterations.ToList();

        // Assert
        result.IsSuccess.Should().BeTrue();
        iterations.Count.Should().Be(expectedIterations);
        iterations[0].Name.Should().Be("Iteration 1");
        iterations[0].Category.Should().Be(IterationCategory.Development);
        iterations[0].DateRange.Start.Should().Be(new LocalDate(2023, 10, 2));
        iterations[0].DateRange.End.Should().Be(new LocalDate(2023, 10, 15));
        iterations[1].Name.Should().Be("Iteration 2");
        iterations[1].Category.Should().Be(IterationCategory.Development);
        iterations[1].DateRange.Start.Should().Be(new LocalDate(2023, 10, 16));
        iterations[1].DateRange.End.Should().Be(new LocalDate(2023, 10, 29));
        iterations[2].Name.Should().Be("Iteration 3");
        iterations[2].Category.Should().Be(IterationCategory.Development);
        iterations[2].DateRange.Start.Should().Be(new LocalDate(2023, 10, 30));
        iterations[2].DateRange.End.Should().Be(new LocalDate(2023, 11, 12));
        iterations[3].Name.Should().Be("Iteration 4");
        iterations[3].Category.Should().Be(IterationCategory.Development);
        iterations[3].DateRange.Start.Should().Be(new LocalDate(2023, 11, 13));
        iterations[3].DateRange.End.Should().Be(new LocalDate(2023, 11, 26));
        iterations[4].Name.Should().Be("Iteration 5");
        iterations[4].Category.Should().Be(IterationCategory.InnovationAndPlanning);
        iterations[4].DateRange.Start.Should().Be(new LocalDate(2023, 11, 27));
        iterations[4].DateRange.End.Should().Be(new LocalDate(2023, 11, 27));
    }

    [Fact]
    public void InitializeIterations_WhenIterationsAlreadyExist_ReturnsFailure()
    {
        // Arrange
        var sut = _planningIntervalFaker.Generate();

        List<UpsertPlanningIntervalIteration> iterations =
        [
            UpsertPlanningIntervalIteration.Create(null, "Iteration 1", IterationCategory.Development, new LocalDateRange(new LocalDate(2023, 1, 1), new LocalDate(2023, 1, 31))),
            UpsertPlanningIntervalIteration.Create(null, "Iteration 2", IterationCategory.Development, new LocalDateRange(new LocalDate(2023, 2, 1), new LocalDate(2023, 2, 28))),
            UpsertPlanningIntervalIteration.Create(null, "Iteration 3", IterationCategory.InnovationAndPlanning, new LocalDateRange(new LocalDate(2023, 3, 1), new LocalDate(2023, 3, 31))),
        ];

        sut.ManageDates(new LocalDateRange(new LocalDate(2023, 1, 1), new LocalDate(2023, 3, 31)), iterations);

        // Act
        var result = sut.InitializeIterations(4, "Iteration ");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Unable to generate new iterations for a Planning Interval that has iterations.");
    }

    #endregion Initialize Iterations

    #region Add Iteration

    [Fact]
    public void AddIteration_WhenAddingValidIteration_ReturnsSuccess()
    {
        // Arrange
        var piDates = new LocalDateRange(new LocalDate(2023, 1, 1), new LocalDate(2023, 3, 31));
        var sut = _planningIntervalFaker.WithDateRange(piDates).Generate();

        // Act
        var result = sut.AddIteration("Iteration 1", IterationCategory.Development, new LocalDateRange(new LocalDate(2023, 1, 1), new LocalDate(2023, 1, 31)));
        var iterations = sut.Iterations.ToList();

        // Assert
        result.IsSuccess.Should().BeTrue();
        iterations.Count.Should().Be(1);
        iterations[0].Name.Should().Be("Iteration 1");
        iterations[0].Category.Should().Be(IterationCategory.Development);
        iterations[0].DateRange.Start.Should().Be(new LocalDate(2023, 1, 1));
        iterations[0].DateRange.End.Should().Be(new LocalDate(2023, 1, 31));
    }

    [Fact]
    public void AddIteration_WithDuplicateName_ReturnsFailure()
    {
        // Arrange
        var piDates = new LocalDateRange(new LocalDate(2023, 1, 1), new LocalDate(2023, 3, 31));
        var sut = _planningIntervalFaker.WithDateRange(piDates).Generate();

        sut.AddIteration("Iteration 1", IterationCategory.Development, new LocalDateRange(new LocalDate(2023, 1, 1), new LocalDate(2023, 1, 31)));

        // Act
        var result = sut.AddIteration("Iteration 1", IterationCategory.Development, new LocalDateRange(new LocalDate(2023, 1, 1), new LocalDate(2023, 1, 31)));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Iteration name already exists.");
    }

    [Fact]
    public void AddIteration_WithOverlappingDateRange_ReturnsFailure()
    {
        // Arrange
        var piDates = new LocalDateRange(new LocalDate(2023, 1, 1), new LocalDate(2023, 3, 31));
        var sut = _planningIntervalFaker.WithDateRange(piDates).Generate();

        sut.AddIteration("Iteration 1", IterationCategory.Development, new LocalDateRange(new LocalDate(2023, 1, 1), new LocalDate(2023, 1, 31)));

        // Act
        var result = sut.AddIteration("Iteration 2", IterationCategory.Development, new LocalDateRange(new LocalDate(2023, 1, 31), new LocalDate(2023, 2, 15)));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Iteration date range overlaps with existing iteration date range.");
    }

    [Fact]
    public void AddIteration_WithStartDateBeforePIStartDate_ReturnsFailure()
    {
        // Arrange
        var piDates = new LocalDateRange(new LocalDate(2023, 1, 1), new LocalDate(2023, 3, 31));
        var sut = _planningIntervalFaker.WithDateRange(piDates).Generate();

        // Act
        var result = sut.AddIteration("Iteration 1", IterationCategory.Development, new LocalDateRange(new LocalDate(2020, 12, 31), new LocalDate(2023, 1, 30)));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Iteration date range cannot start before the Planning Interval date range.");
    }

    [Fact]
    public void AddIteration_WithEndDateAfterPIEndDate_ReturnsFailure()
    {
        // Arrange
        var piDates = new LocalDateRange(new LocalDate(2023, 1, 1), new LocalDate(2023, 3, 31));
        var sut = _planningIntervalFaker.WithDateRange(piDates).Generate();

        // Act
        var result = sut.AddIteration("Iteration 1", IterationCategory.Development, new LocalDateRange(new LocalDate(2023, 3, 20), new LocalDate(2023, 4, 1)));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Iteration date range cannot end after the Planning Interval date range.");
    }

    #endregion Add Iteration

    #region ManageDates

    [Fact]
    public void ManageDates_WhenDatesAreValidAndNoIterations_ReturnsSuccess()
    {
        // Arrange
        var sut = _planningIntervalFaker.Generate();
        var expectedStartDate = new LocalDate(2023, 1, 1);
        var expectedEndDate = new LocalDate(2023, 3, 31);

        // Act
        var result = sut.ManageDates(new LocalDateRange(expectedStartDate, expectedEndDate), []);
        var iterations = sut.Iterations.ToList();

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DateRange.Start.Should().Be(expectedStartDate);
        sut.DateRange.End.Should().Be(expectedEndDate);
        iterations.Count.Should().Be(0);
    }

    [Fact]
    public void ManageDates_WhenAddingInitailIterations_ReturnsSuccess()
    {
        // Arrange
        var sut = _planningIntervalFaker.Generate();
        var expectedStartDate = new LocalDate(2023, 1, 1);
        var expectedEndDate = new LocalDate(2023, 3, 31);

        var iterations = new List<UpsertPlanningIntervalIteration>
        {
            UpsertPlanningIntervalIteration.Create(null, "Iteration 1", IterationCategory.Development, new LocalDateRange(new LocalDate(2023, 1, 1), new LocalDate(2023, 1, 31))),
            UpsertPlanningIntervalIteration.Create(null, "Iteration 2", IterationCategory.Development, new LocalDateRange(new LocalDate(2023, 2, 1), new LocalDate(2023, 2, 28))),
            UpsertPlanningIntervalIteration.Create(null, "Iteration 3", IterationCategory.InnovationAndPlanning, new LocalDateRange(new LocalDate(2023, 3, 1), new LocalDate(2023, 3, 31)))
        };

        // Act
        var result = sut.ManageDates(new LocalDateRange(expectedStartDate, expectedEndDate), iterations);
        var updatedIterations = sut.Iterations.ToList();

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DateRange.Start.Should().Be(expectedStartDate);
        sut.DateRange.End.Should().Be(expectedEndDate);
        updatedIterations.Count.Should().Be(3);
        updatedIterations[0].Name.Should().Be("Iteration 1");
        updatedIterations[0].Category.Should().Be(IterationCategory.Development);
        updatedIterations[0].DateRange.Start.Should().Be(new LocalDate(2023, 1, 1));
        updatedIterations[0].DateRange.End.Should().Be(new LocalDate(2023, 1, 31));
        updatedIterations[1].Name.Should().Be("Iteration 2");
        updatedIterations[1].Category.Should().Be(IterationCategory.Development);
        updatedIterations[1].DateRange.Start.Should().Be(new LocalDate(2023, 2, 1));
        updatedIterations[1].DateRange.End.Should().Be(new LocalDate(2023, 2, 28));
        updatedIterations[2].Name.Should().Be("Iteration 3");
        updatedIterations[2].Category.Should().Be(IterationCategory.InnovationAndPlanning);
    }

    [Fact]
    public void ManageDates_WhenDuplicateNames_ReturnsFailure()
    {
        // Arrange
        var startDate = new LocalDate(2023, 1, 1);
        var endDate = new LocalDate(2023, 3, 31);

        var sut = _planningIntervalFaker
            .WithIterations(new LocalDateRange(startDate, endDate), 2, "Iteration ")
            .Generate();

        var iterations = sut.Iterations
            .Select(i => UpsertPlanningIntervalIteration.Create(i.Id, i.Name, i.Category, i.DateRange))
            .ToList();

        iterations.Last().Name = iterations.First().Name;

        // Act
        var result = sut.ManageDates(new LocalDateRange(sut.DateRange.Start, sut.DateRange.End), iterations);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Iteration names must be unique within the PI.");
    }

    [Fact]
    public void ManageDates_WhenStartingBeforePI_ReturnsFailure()
    {
        // Arrange
        var startDate = new LocalDate(2023, 1, 1);
        var endDate = new LocalDate(2023, 3, 31);

        var sut = _planningIntervalFaker
            .WithIterations(new LocalDateRange(startDate, endDate), 2, "Iteration ")
            .Generate();

        var iterations = sut.Iterations
            .Select(i => UpsertPlanningIntervalIteration.Create(i.Id, i.Name, i.Category, i.DateRange))
            .ToList();

        iterations.First().DateRange = new LocalDateRange(startDate.Plus(Period.FromDays(-1)), iterations.First().DateRange.End);

        // Act
        var result = sut.ManageDates(new LocalDateRange(sut.DateRange.Start, sut.DateRange.End), iterations);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Iteration date ranges cannot start before the Planning Interval date range.");
    }

    [Fact]
    public void ManageDates_WhenEndingAfterPI_ReturnsFailure()
    {
        // Arrange
        var startDate = new LocalDate(2023, 1, 1);
        var endDate = new LocalDate(2023, 3, 31);

        var sut = _planningIntervalFaker
            .WithIterations(new LocalDateRange(startDate, endDate), 2, "Iteration ")
            .Generate();

        var iterations = sut.Iterations
            .Select(i => UpsertPlanningIntervalIteration.Create(i.Id, i.Name, i.Category, i.DateRange))
            .ToList();

        iterations.Last().DateRange = new LocalDateRange(iterations.Last().DateRange.Start, endDate.Plus(Period.FromDays(1)));

        // Act
        var result = sut.ManageDates(new LocalDateRange(sut.DateRange.Start, sut.DateRange.End), iterations);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Iteration date ranges cannot end after the Planning Interval date range.");
    }

    [Fact]
    public void ManageDates_WhenOverlappingDates_ReturnsFailure()
    {
        // Arrange
        var startDate = new LocalDate(2023, 1, 1);
        var endDate = new LocalDate(2023, 3, 31);

        var sut = _planningIntervalFaker
            .WithIterations(new LocalDateRange(startDate, endDate), 2, "Iteration ")
            .Generate();

        var iterations = sut.Iterations
            .Select(i => UpsertPlanningIntervalIteration.Create(i.Id, i.Name, i.Category, i.DateRange))
            .ToList();

        var secondIteration = iterations[1];

        secondIteration.DateRange = new LocalDateRange(secondIteration.DateRange.Start.Plus(Period.FromDays(-1)), secondIteration.DateRange.End);

        // Act
        var result = sut.ManageDates(new LocalDateRange(sut.DateRange.Start, sut.DateRange.End), iterations);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Iteration date ranges cannot overlap.");
    }

    // TODO: Add tests updating and removing iterations

    #endregion ManageDates

    #region Sprint Mappings

    [Fact]
    public void MapSprintToIteration_WithValidSprint_ReturnsSuccess()
    {
        // Arrange
        var piDates = new LocalDateRange(new LocalDate(2023, 1, 1), new LocalDate(2023, 3, 31));
        var teamId = Guid.NewGuid();
        var sut = _planningIntervalFaker
            .WithDateRange(piDates)
            .WithTeams(teamId)
            .WithIterations(piDates, 2, "Iteration ")
            .Generate();

        var iterationId = sut.Iterations.First().Id;
        var sprint = new IterationFaker()
            .AsSprint()
            .WithTeamId(teamId)
            .Generate();

        // Act
        var result = sut.MapSprintToIteration(iterationId, sprint);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.IterationSprints.Should().HaveCount(1);
        sut.IterationSprints.First().SprintId.Should().Be(sprint.Id);
        sut.IterationSprints.First().PlanningIntervalIterationId.Should().Be(iterationId);
    }

    [Fact]
    public void MapSprintToIteration_WithNonSprintType_ReturnsFailure()
    {
        // Arrange
        var piDates = new LocalDateRange(new LocalDate(2023, 1, 1), new LocalDate(2023, 3, 31));
        var teamId = Guid.NewGuid();
        var sut = _planningIntervalFaker
            .WithDateRange(piDates)
            .WithTeams(teamId)
            .WithIterations(piDates, 2, "Iteration ")
            .Generate();

        var iterationId = sut.Iterations.First().Id;
        var iteration = new IterationFaker()
            .AsIteration() // Not a sprint
            .WithTeamId(teamId)
            .Generate();

        // Act
        var result = sut.MapSprintToIteration(iterationId, iteration);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only sprints of type Sprint can be mapped to iterations.");
        sut.IterationSprints.Should().BeEmpty();
    }

    [Fact]
    public void MapSprintToIteration_WithSprintNotBelongingToTeamInPI_ReturnsFailure()
    {
        // Arrange
        var piDates = new LocalDateRange(new LocalDate(2023, 1, 1), new LocalDate(2023, 3, 31));
        var teamId = Guid.NewGuid();
        var otherTeamId = Guid.NewGuid();
        var sut = _planningIntervalFaker
            .WithDateRange(piDates)
            .WithTeams(teamId) // Only teamId is in the PI
            .WithIterations(piDates, 2, "Iteration ")
            .Generate();

        var iterationId = sut.Iterations.First().Id;
        var sprint = new IterationFaker()
            .AsSprint()
            .WithTeamId(otherTeamId) // Sprint belongs to different team
            .Generate();

        // Act
        var result = sut.MapSprintToIteration(iterationId, sprint);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The sprint must belong to a team that is part of this Planning Interval.");
        sut.IterationSprints.Should().BeEmpty();
    }

    [Fact]
    public void MapSprintToIteration_WithSprintWithoutTeam_ReturnsFailure()
    {
        // Arrange
        var piDates = new LocalDateRange(new LocalDate(2023, 1, 1), new LocalDate(2023, 3, 31));
        var teamId = Guid.NewGuid();
        var sut = _planningIntervalFaker
            .WithDateRange(piDates)
            .WithTeams(teamId)
            .WithIterations(piDates, 2, "Iteration ")
            .Generate();

        var iterationId = sut.Iterations.First().Id;
        var sprint = new IterationFaker()
            .AsSprint()
            .WithTeamId(null) // No team assigned
            .Generate();

        // Act
        var result = sut.MapSprintToIteration(iterationId, sprint);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The sprint must belong to a team that is part of this Planning Interval.");
        sut.IterationSprints.Should().BeEmpty();
    }

    [Fact]
    public void MapSprintToIteration_WithNonExistentIteration_ReturnsFailure()
    {
        // Arrange
        var piDates = new LocalDateRange(new LocalDate(2023, 1, 1), new LocalDate(2023, 3, 31));
        var teamId = Guid.NewGuid();
        var sut = _planningIntervalFaker
            .WithDateRange(piDates)
            .WithTeams(teamId)
            .WithIterations(piDates, 2, "Iteration ")
            .Generate();

        var nonExistentIterationId = Guid.NewGuid();
        var sprint = new IterationFaker()
            .AsSprint()
            .WithTeamId(teamId)
            .Generate();

        // Act
        var result = sut.MapSprintToIteration(nonExistentIterationId, sprint);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be($"Iteration {nonExistentIterationId} not found in this Planning Interval.");
        sut.IterationSprints.Should().BeEmpty();
    }

    [Fact]
    public void MapSprintToIteration_WhenSprintAlreadyMappedToSameIteration_IsIdempotent()
    {
        // Arrange
        var piDates = new LocalDateRange(new LocalDate(2023, 1, 1), new LocalDate(2023, 3, 31));
        var teamId = Guid.NewGuid();
        var sut = _planningIntervalFaker
            .WithDateRange(piDates)
            .WithTeams(teamId)
            .WithIterations(piDates, 2, "Iteration ")
            .Generate();

        var iterationId = sut.Iterations.First().Id;
        var sprint = new IterationFaker().AsSprint().WithTeamId(teamId).Generate();

        // Map sprint first time
        var firstResult = sut.MapSprintToIteration(iterationId, sprint);
        firstResult.IsSuccess.Should().BeTrue();

        // Act - Map same sprint to same iteration again
        var secondResult = sut.MapSprintToIteration(iterationId, sprint);

        // Assert - Operation is idempotent, should succeed
        secondResult.IsSuccess.Should().BeTrue();
        sut.IterationSprints.Should().HaveCount(1, "mapping same sprint twice should not create duplicate");
        sut.IterationSprints.First().SprintId.Should().Be(sprint.Id);
    }

    [Fact]
    public void MapSprintToIteration_WithSprintAlreadyMappedToDifferentIteration_MovesSprintToNewIteration()
    {
        // Arrange
        var piDates = new LocalDateRange(new LocalDate(2023, 1, 1), new LocalDate(2023, 3, 31));
        var teamId = Guid.NewGuid();
        var sut = _planningIntervalFaker
            .WithDateRange(piDates)
            .WithTeams(teamId)
            .WithIterations(piDates, 2, "Iteration ")
            .Generate();

        var firstIterationId = sut.Iterations.First().Id;
        var secondIterationId = sut.Iterations.Last().Id;
        var sprint = new IterationFaker()
            .AsSprint()
            .WithTeamId(teamId)
            .Generate();

        sut.MapSprintToIteration(firstIterationId, sprint);
        sut.IterationSprints.Should().HaveCount(1);
        sut.IterationSprints.First().PlanningIntervalIterationId.Should().Be(firstIterationId);

        // Act - Map same sprint to second iteration (should move it)
        var result = sut.MapSprintToIteration(secondIterationId, sprint);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.IterationSprints.Should().HaveCount(1, "sprint should be moved, not duplicated");
        sut.IterationSprints.First().SprintId.Should().Be(sprint.Id);
        sut.IterationSprints.First().PlanningIntervalIterationId.Should().Be(secondIterationId, "sprint should now be mapped to the second iteration");
    }

    [Fact]
    public void MapSprintToIteration_WithTeamAlreadyHavingSprintInIteration_ReplacesExistingSprint()
    {
        // Arrange
        var piDates = new LocalDateRange(new LocalDate(2023, 1, 1), new LocalDate(2023, 3, 31));
        var teamId = Guid.NewGuid();
        var sut = _planningIntervalFaker
            .WithDateRange(piDates)
            .WithTeams(teamId)
            .WithIterations(piDates, 2, "Iteration ")
            .Generate();

        var iterationId = sut.Iterations.First().Id;
        var sprint1 = new IterationFaker().AsSprint().WithTeamId(teamId).Generate();
        var sprint2 = new IterationFaker().AsSprint().WithTeamId(teamId).Generate();

        // Map first sprint successfully
        sut.MapSprintToIteration(iterationId, sprint1);

        // Set up Sprint navigation property (simulates EF Core loading)
        foreach (var mapping in sut.IterationSprints)
        {
            if (mapping.SprintId == sprint1.Id)
                mapping.SetPrivate(m => m.Sprint, sprint1);
        }

        sut.IterationSprints.Should().HaveCount(1);
        sut.IterationSprints.First().SprintId.Should().Be(sprint1.Id);

        // Act - Map second sprint from same team to same iteration (should replace)
        var result = sut.MapSprintToIteration(iterationId, sprint2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.IterationSprints.Should().HaveCount(1, "team can only have one sprint per iteration");
        sut.IterationSprints.First().SprintId.Should().Be(sprint2.Id, "new sprint should replace the old one");
    }

    [Fact]
    public void UnmapSprint_WithExistingSprint_ReturnsSuccess()
    {
        // Arrange
        var piDates = new LocalDateRange(new LocalDate(2023, 1, 1), new LocalDate(2023, 3, 31));
        var teamId = Guid.NewGuid();
        var sut = _planningIntervalFaker
            .WithDateRange(piDates)
            .WithTeams(teamId)
            .WithIterations(piDates, 2, "Iteration ")
            .Generate();

        var iterationId = sut.Iterations.First().Id;
        var sprint = new IterationFaker()
            .AsSprint()
            .WithTeamId(teamId)
            .Generate();

        sut.MapSprintToIteration(iterationId, sprint);

        // Act
        var result = sut.UnmapSprint(sprint.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.IterationSprints.Should().BeEmpty();
    }

    [Fact]
    public void UnmapSprint_WithNonExistentSprint_ReturnsFailure()
    {
        // Arrange
        var piDates = new LocalDateRange(new LocalDate(2023, 1, 1), new LocalDate(2023, 3, 31));
        var sut = _planningIntervalFaker
            .WithDateRange(piDates)
            .WithIterations(piDates, 2, "Iteration ")
            .Generate();

        var nonExistentSprintId = Guid.NewGuid();

        // Act
        var result = sut.UnmapSprint(nonExistentSprintId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Sprint mapping not found in this Planning Interval.");
    }

    [Fact]
    public void GetSprintsForIteration_WithMultipleSprints_ReturnsCorrectSprints()
    {
        // Arrange
        var piDates = new LocalDateRange(new LocalDate(2023, 1, 1), new LocalDate(2023, 3, 31));
        var teamId = Guid.NewGuid();
        var sut = _planningIntervalFaker
            .WithDateRange(piDates)
            .WithTeams(teamId)
            .WithIterations(piDates, 2, "Iteration ")
            .Generate();

        var firstIterationId = sut.Iterations.First().Id;
        var secondIterationId = sut.Iterations.Last().Id;

        var sprint1 = new IterationFaker().AsSprint().WithTeamId(teamId).Generate();
        var sprint2 = new IterationFaker().AsSprint().WithTeamId(teamId).Generate();
        var sprint3 = new IterationFaker().AsSprint().WithTeamId(teamId).Generate();

        sut.MapSprintToIteration(firstIterationId, sprint1);
        sut.MapSprintToIteration(firstIterationId, sprint2);
        sut.MapSprintToIteration(secondIterationId, sprint3);

        // Act
        var firstIterationSprints = sut.GetSprintsForIteration(firstIterationId);
        var secondIterationSprints = sut.GetSprintsForIteration(secondIterationId);

        // Assert
        firstIterationSprints.Should().HaveCount(2);
        firstIterationSprints.Should().Contain(s => s.SprintId == sprint1.Id);
        firstIterationSprints.Should().Contain(s => s.SprintId == sprint2.Id);

        secondIterationSprints.Should().HaveCount(1);
        secondIterationSprints.First().SprintId.Should().Be(sprint3.Id);
    }

    [Fact]
    public void GetSprintsForIteration_WithNoSprints_ReturnsEmptyCollection()
    {
        // Arrange
        var piDates = new LocalDateRange(new LocalDate(2023, 1, 1), new LocalDate(2023, 3, 31));
        var sut = _planningIntervalFaker
            .WithDateRange(piDates)
            .WithIterations(piDates, 2, "Iteration ")
            .Generate();

        var iterationId = sut.Iterations.First().Id;

        // Act
        var sprints = sut.GetSprintsForIteration(iterationId);

        // Assert
        sprints.Should().BeEmpty();
    }

    [Fact]
    public void ManageTeams_WhenRemovingTeam_RemovesAssociatedSprintMappings()
    {
        // Arrange
        var piDates = new LocalDateRange(new LocalDate(2023, 1, 1), new LocalDate(2023, 3, 31));
        var team1Id = Guid.NewGuid();
        var team2Id = Guid.NewGuid();
        var sut = _planningIntervalFaker
            .WithDateRange(piDates)
            .WithTeams(team1Id, team2Id)
            .WithIterations(piDates, 2, "Iteration ")
            .Generate();

        var iterationId = sut.Iterations.First().Id;
        var team1Sprint = new IterationFaker().AsSprint().WithTeamId(team1Id).Generate();
        var team2Sprint = new IterationFaker().AsSprint().WithTeamId(team2Id).Generate();

        sut.MapSprintToIteration(iterationId, team1Sprint);
        sut.MapSprintToIteration(iterationId, team2Sprint);

        // Set up the Sprint navigation properties (simulates EF Core loading)
        foreach (var mapping in sut.IterationSprints)
        {
            if (mapping.SprintId == team1Sprint.Id)
                mapping.SetPrivate(m => m.Sprint, team1Sprint);
            if (mapping.SprintId == team2Sprint.Id)
                mapping.SetPrivate(m => m.Sprint, team2Sprint);
        }

        // Act - Remove team1, keep team2
        var result = sut.ManageTeams(new[] { team2Id });

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Teams.Should().HaveCount(1);
        sut.Teams.First().TeamId.Should().Be(team2Id);
        sut.IterationSprints.Should().HaveCount(1);
        sut.IterationSprints.First().SprintId.Should().Be(team2Sprint.Id);
    }

    #endregion Sprint Mappings

    #region Objectives

    [Fact]
    public void CreateObjective_WhenUnlocked_AddsObjectiveWithTheTeamsType()
    {
        // Arrange
        var team = new PlanningTeamFaker(TeamType.TeamOfTeams).Generate();
        var sut = _planningIntervalFaker.Generate();
        var start = sut.DateRange.Start;
        var target = sut.DateRange.End;

        // Act
        var result = sut.CreateObjective(team, "  Ship it  ", "  ", true, start, target, 2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var objective = sut.Objectives.Should().ContainSingle().Subject;
        objective.Should().BeSameAs(result.Value);
        objective.PlanningIntervalId.Should().Be(sut.Id);
        objective.TeamId.Should().Be(team.Id);
        objective.Name.Should().Be("Ship it");
        objective.Description.Should().BeNull();
        objective.Type.Should().Be(PlanningIntervalObjectiveType.TeamOfTeams);
        objective.Status.Should().Be(ObjectiveStatus.NotStarted);
        objective.Progress.Should().Be(0);
        objective.IsStretch.Should().BeTrue();
        objective.StartDate.Should().Be(start);
        objective.TargetDate.Should().Be(target);
        objective.ClosedDate.Should().BeNull();
        objective.Order.Should().Be(2);
    }

    [Fact]
    public void CreateObjective_WhenLocked_Fails()
    {
        // Arrange
        var team = new PlanningTeamFaker(TeamType.Team).Generate();
        var sut = _planningIntervalFaker.WithObjectivesLocked(true).Generate();

        // Act
        var result = sut.CreateObjective(team, "Ship it", null, false, null, null, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("locked");
        sut.Objectives.Should().BeEmpty();
    }

    [Fact]
    public void CreateObjective_WhenNameIsBlank_Fails()
    {
        // Arrange
        var team = new PlanningTeamFaker(TeamType.Team).Generate();
        var sut = _planningIntervalFaker.Generate();

        // Act
        var result = sut.CreateObjective(team, "   ", null, false, null, null, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        sut.Objectives.Should().BeEmpty();
    }

    [Fact]
    public void ImportObjective_KeepsTheGivenStatusProgressAndClosedDate()
    {
        // Arrange
        var team = new PlanningTeamFaker(TeamType.Team).Generate();
        var sut = _planningIntervalFaker.Generate();
        var closed = _dateTimeProvider.Now;

        // Act
        var result = sut.ImportObjective(team, "Imported", "From a file", ObjectiveStatus.Completed, 100, false, null, null, closed, 7);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var objective = sut.Objectives.Single();
        objective.Status.Should().Be(ObjectiveStatus.Completed);
        objective.Progress.Should().Be(100);
        objective.ClosedDate.Should().Be(closed);
        objective.Order.Should().Be(7);
        objective.Type.Should().Be(PlanningIntervalObjectiveType.Team);
    }

    [Fact]
    public void ImportObjective_ClampsProgressToTheValidRange()
    {
        // Arrange
        var team = new PlanningTeamFaker(TeamType.Team).Generate();
        var sut = _planningIntervalFaker.Generate();

        // Act
        var result = sut.ImportObjective(team, "Imported", null, ObjectiveStatus.InProgress, 150, false, null, null, null, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Objectives.Single().Progress.Should().Be(100);
    }

    [Fact]
    public void UpdateObjective_WhenUnlocked_UpdatesEveryField()
    {
        // Arrange
        var team = new PlanningTeamFaker(TeamType.Team).Generate();
        var sut = _planningIntervalFaker.WithObjectives(team, 1).Generate();
        var objective = sut.Objectives.Single();
        var start = sut.DateRange.Start;
        var target = sut.DateRange.End;

        // Act
        var result = sut.UpdateObjective(objective.Id, "Renamed", "Described", ObjectiveStatus.InProgress, 40, start, target, true, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        objective.Name.Should().Be("Renamed");
        objective.Description.Should().Be("Described");
        objective.Status.Should().Be(ObjectiveStatus.InProgress);
        objective.Progress.Should().Be(40);
        objective.StartDate.Should().Be(start);
        objective.TargetDate.Should().Be(target);
        objective.IsStretch.Should().BeTrue();
        objective.ClosedDate.Should().BeNull();
    }

    [Fact]
    public void UpdateObjective_WhenLocked_FreezesNameAndStretchButUpdatesTheRest()
    {
        // Arrange
        var team = new PlanningTeamFaker(TeamType.Team).Generate();
        var sut = _planningIntervalFaker.WithObjectives(team, 1).WithObjectivesLocked(true).Generate();
        var objective = sut.Objectives.Single();
        var originalName = objective.Name;
        var originalStretch = objective.IsStretch;

        // Act
        var result = sut.UpdateObjective(objective.Id, "Renamed", "Described", ObjectiveStatus.InProgress, 40, null, null, !originalStretch, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        objective.Name.Should().Be(originalName);
        objective.IsStretch.Should().Be(originalStretch);
        objective.Description.Should().Be("Described");
        objective.Status.Should().Be(ObjectiveStatus.InProgress);
        objective.Progress.Should().Be(40);
    }

    [Theory]
    [InlineData(ObjectiveStatus.Completed)]
    [InlineData(ObjectiveStatus.Canceled)]
    [InlineData(ObjectiveStatus.Missed)]
    public void UpdateObjective_WhenClosing_SetsClosedDate(ObjectiveStatus closedStatus)
    {
        // Arrange
        var team = new PlanningTeamFaker(TeamType.Team).Generate();
        var sut = _planningIntervalFaker.WithObjectives(team, 1).Generate();
        var objective = sut.Objectives.Single();
        var now = _dateTimeProvider.Now;

        // Act
        var result = sut.UpdateObjective(objective.Id, objective.Name, null, closedStatus, 100, null, null, false, now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        objective.Status.Should().Be(closedStatus);
        objective.ClosedDate.Should().Be(now);
    }

    [Fact]
    public void UpdateObjective_WhenReopening_ClearsClosedDate()
    {
        // Arrange
        var team = new PlanningTeamFaker(TeamType.Team).Generate();
        var sut = _planningIntervalFaker.WithObjectives(team, 1).Generate();
        var objective = sut.Objectives.Single();
        sut.UpdateObjective(objective.Id, objective.Name, null, ObjectiveStatus.Completed, 100, null, null, false, _dateTimeProvider.Now);

        // Act
        var result = sut.UpdateObjective(objective.Id, objective.Name, null, ObjectiveStatus.InProgress, 60, null, null, false, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        objective.Status.Should().Be(ObjectiveStatus.InProgress);
        objective.ClosedDate.Should().BeNull();
    }

    [Fact]
    public void UpdateObjective_WhenMovingBetweenClosedStatuses_RestampsClosedDate()
    {
        // Arrange
        var team = new PlanningTeamFaker(TeamType.Team).Generate();
        var sut = _planningIntervalFaker.WithObjectives(team, 1).Generate();
        var objective = sut.Objectives.Single();
        var firstClose = _dateTimeProvider.Now;
        var secondClose = firstClose.Plus(Duration.FromDays(1));
        sut.UpdateObjective(objective.Id, objective.Name, null, ObjectiveStatus.Missed, 0, null, null, false, firstClose);

        // Act
        sut.UpdateObjective(objective.Id, objective.Name, null, ObjectiveStatus.Canceled, 0, null, null, false, secondClose);

        // Assert
        objective.ClosedDate.Should().Be(secondClose);
    }

    [Fact]
    public void UpdateObjective_WhenNotFound_Fails()
    {
        // Arrange
        var team = new PlanningTeamFaker(TeamType.Team).Generate();
        var sut = _planningIntervalFaker.WithObjectives(team, 1).Generate();

        // Act
        var result = sut.UpdateObjective(Guid.NewGuid(), "Renamed", null, ObjectiveStatus.InProgress, 0, null, null, false, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public void UpdateObjectivesOrder_SetsEachObjectivesOrder()
    {
        // Arrange
        var team = new PlanningTeamFaker(TeamType.Team).Generate();
        var sut = _planningIntervalFaker.WithObjectives(team, 3).Generate();
        var ids = sut.Objectives.Select(o => o.Id).ToArray();
        var orders = new Dictionary<Guid, int?> { [ids[0]] = 3, [ids[1]] = null, [ids[2]] = 1 };

        // Act
        var result = sut.UpdateObjectivesOrder(orders);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Objectives.Single(o => o.Id == ids[0]).Order.Should().Be(3);
        sut.Objectives.Single(o => o.Id == ids[1]).Order.Should().BeNull();
        sut.Objectives.Single(o => o.Id == ids[2]).Order.Should().Be(1);
    }

    [Fact]
    public void UpdateObjectivesOrder_WhenAnIdIsNotOnThisInterval_ChangesNothing()
    {
        // Arrange
        var team = new PlanningTeamFaker(TeamType.Team).Generate();
        var sut = _planningIntervalFaker.WithObjectives(team, 2).Generate();
        var known = sut.Objectives.First();
        var orders = new Dictionary<Guid, int?> { [known.Id] = 5, [Guid.NewGuid()] = 1 };

        // Act
        var result = sut.UpdateObjectivesOrder(orders);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        known.Order.Should().BeNull();
    }

    #endregion Objectives
}


