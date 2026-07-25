using Wayd.Common.Domain.Enums.Work;
using Wayd.Planning.Domain.Models.StoryMaps;

namespace Wayd.Planning.Domain.Tests.Sut.Models.StoryMaps;

public class StoryMapTests
{
    // A map created the real way starts empty; seed a first goal with a step for the many tests that
    // need a graph to operate on.
    private static StoryMap CreateMap()
    {
        var map = StoryMap.Create("My Map", "A description", Guid.NewGuid().ToString()).Value;
        var goal = map.AddGoal("First Goal").Value;
        map.AddStep(goal.Id, "First Step");
        return map;
    }

    #region Create

    [Fact]
    public void Create_ValidParameters_ShouldReturnActiveMap()
    {
        // Arrange
        var ownerId = Guid.NewGuid().ToString();

        // Act
        var result = StoryMap.Create("My Map", "A description", ownerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var map = result.Value;
        map.Name.Should().Be("My Map");
        map.Description.Should().Be("A description");
        map.OwnerId.Should().Be(ownerId);
        map.Status.Should().Be(WorkStatusCategory.Active);
    }

    [Fact]
    public void Create_ValidParameters_ShouldStartWithNoGoals()
    {
        // Act
        var map = StoryMap.Create("My Map", "A description", Guid.NewGuid().ToString()).Value;

        // Assert — a new map is empty; the user adds the first goal from the board's empty state.
        map.Goals.Should().BeEmpty();
    }

    [Fact]
    public void Create_ValidParameters_ShouldSeedSingleDefaultSwimLaneNamedTasks()
    {
        // Act
        var map = StoryMap.Create("My Map", "A description", Guid.NewGuid().ToString()).Value;

        // Assert
        map.SwimLanes.Should().ContainSingle();
        var lane = map.SwimLanes.Single();
        lane.IsDefault.Should().BeTrue();
        lane.Name.Should().Be("Tasks");
        lane.Order.Should().Be(0);
    }

    [Fact]
    public void Create_WhitespaceName_ShouldReturnFailure()
    {
        // Act
        var result = StoryMap.Create("   ", null, Guid.NewGuid().ToString());

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region Map lifecycle

    [Fact]
    public void Update_ValidNameAndDescription_ShouldUpdate()
    {
        // Arrange
        var map = CreateMap();

        // Act
        var result = map.Update("New Name", "New description");

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.Name.Should().Be("New Name");
        map.Description.Should().Be("New description");
    }

    [Fact]
    public void Update_WhitespaceName_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();

        // Act
        var result = map.Update("   ", "desc");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ChangeOwner_ValidOwner_ShouldReassign()
    {
        // Arrange
        var map = CreateMap();
        var newOwner = Guid.NewGuid().ToString();

        // Act
        var result = map.ChangeOwner(newOwner);

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.OwnerId.Should().Be(newOwner);
    }

    [Fact]
    public void ChangeOwner_WhitespaceOwner_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();

        // Act
        var result = map.ChangeOwner("   ");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Archive_ActiveMap_ShouldSetStatusToRemoved()
    {
        // Arrange
        var map = CreateMap();

        // Act
        var result = map.Archive();

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.Status.Should().Be(WorkStatusCategory.Removed);
    }

    [Fact]
    public void Archive_AlreadyArchived_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();
        map.Archive();

        // Act
        var result = map.Archive();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region Goals

    [Fact]
    public void AddGoal_ValidName_ShouldAppendEmptyGoal()
    {
        // Arrange
        var map = CreateMap();

        // Act
        var result = map.AddGoal("Second Goal");

        // Assert — a new goal starts empty; steps are added to it separately.
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Second Goal");
        result.Value.Order.Should().Be(1);
        result.Value.Steps.Should().BeEmpty();
        map.Goals.Should().HaveCount(2);
    }

    [Fact]
    public void AddGoal_WhitespaceName_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();

        // Act
        var result = map.AddGoal("   ");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RenameGoal_ExistingGoal_ShouldRename()
    {
        // Arrange
        var map = CreateMap();
        var goalId = map.Goals.Single().Id;

        // Act
        var result = map.RenameGoal(goalId, "Renamed Goal");

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.Goals.Single().Name.Should().Be("Renamed Goal");
    }

    [Fact]
    public void RenameGoal_UnknownGoal_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();

        // Act
        var result = map.RenameGoal(Guid.NewGuid(), "Name");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ReorderGoal_ShouldRenumberContiguously()
    {
        // Arrange
        var map = CreateMap();
        var g1 = map.Goals.Single();
        var g2 = map.AddGoal("Goal 2").Value;
        var g3 = map.AddGoal("Goal 3").Value;

        // Act
        var result = map.ReorderGoal(g3.Id, 0);

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.Goals.Select(x => x.Id).Should().ContainInOrder(g3.Id, g1.Id, g2.Id);
        map.Goals.Select(x => x.Order).Should().ContainInOrder(0, 1, 2);
    }

    [Fact]
    public void DeleteGoal_WhenMoreThanOne_ShouldRemoveAndRenumber()
    {
        // Arrange
        var map = CreateMap();
        var g1 = map.Goals.Single();
        var g2 = map.AddGoal("Goal 2").Value;

        // Act
        var result = map.DeleteGoal(g1.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.Goals.Should().ContainSingle();
        map.Goals.Single().Id.Should().Be(g2.Id);
        map.Goals.Single().Order.Should().Be(0);
    }

    [Fact]
    public void DeleteGoal_LastRemainingGoal_ShouldSucceedAndLeaveMapEmpty()
    {
        // Arrange
        var map = CreateMap();
        var goalId = map.Goals.Single().Id;

        // Act — deleting the last goal returns the map to its empty state.
        var result = map.DeleteGoal(goalId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.Goals.Should().BeEmpty();
    }

    [Fact]
    public void DeleteGoal_UnknownGoal_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();

        // Act
        var result = map.DeleteGoal(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region Steps

    [Fact]
    public void AddStep_ExistingGoal_ShouldAppendStep()
    {
        // Arrange
        var map = CreateMap();
        var goal = map.Goals.Single();

        // Act
        var result = map.AddStep(goal.Id, "Second Step");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Second Step");
        result.Value.Order.Should().Be(1);
        map.Goals.Single().Steps.Should().HaveCount(2);
    }

    [Fact]
    public void AddStep_UnknownGoal_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();

        // Act
        var result = map.AddStep(Guid.NewGuid(), "Step");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RenameStep_ExistingStep_ShouldRename()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;

        // Act
        var result = map.RenameStep(stepId, "Renamed Step");

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.Goals.Single().Steps.Single().Name.Should().Be("Renamed Step");
    }

    [Fact]
    public void RenameStep_UnknownStep_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();

        // Act
        var result = map.RenameStep(Guid.NewGuid(), "Name");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ReorderStep_WithinGoal_ShouldRenumber()
    {
        // Arrange
        var map = CreateMap();
        var goal = map.Goals.Single();
        var s1 = goal.Steps.Single();
        var s2 = map.AddStep(goal.Id, "Step 2").Value;
        var s3 = map.AddStep(goal.Id, "Step 3").Value;

        // Act
        var result = map.ReorderStep(s3.Id, 0);

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.Goals.Single().Steps.Select(x => x.Id).Should().ContainInOrder(s3.Id, s1.Id, s2.Id);
        map.Goals.Single().Steps.Select(x => x.Order).Should().ContainInOrder(0, 1, 2);
    }

    [Fact]
    public void MoveStep_ToAnotherGoal_ShouldRelocateStep()
    {
        // Arrange
        var map = CreateMap();
        var sourceGoal = map.Goals.Single();
        var extraStep = map.AddStep(sourceGoal.Id, "Extra Step").Value;
        var targetGoal = map.AddGoal("Target Goal").Value;

        // Act — target goal starts empty, so it holds just the moved step afterwards.
        var result = map.MoveStep(extraStep.Id, targetGoal.Id, 0);

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.Goals.First(g => g.Id == sourceGoal.Id).Steps.Should().ContainSingle();
        var movedTarget = map.Goals.First(g => g.Id == targetGoal.Id);
        movedTarget.Steps.Should().ContainSingle();
        movedTarget.Steps.Single().Id.Should().Be(extraStep.Id);
    }

    [Fact]
    public void MoveStep_LastStepOutOfGoal_ShouldSucceedAndLeaveSourceEmpty()
    {
        // Arrange
        var map = CreateMap();
        var sourceGoal = map.Goals.Single();
        var onlyStepId = sourceGoal.Steps.Single().Id;
        var targetGoal = map.AddGoal("Target Goal").Value;

        // Act — a goal may be left with no steps.
        var result = map.MoveStep(onlyStepId, targetGoal.Id, 0);

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.Goals.First(g => g.Id == sourceGoal.Id).Steps.Should().BeEmpty();
        map.Goals.First(g => g.Id == targetGoal.Id).Steps.Should().ContainSingle();
    }

    [Fact]
    public void MoveStep_SameGoal_ShouldReorderWithinGoal()
    {
        // Arrange
        var map = CreateMap();
        var goal = map.Goals.Single();
        var s1 = goal.Steps.Single();
        var s2 = map.AddStep(goal.Id, "Step 2").Value;

        // Act
        var result = map.MoveStep(s2.Id, goal.Id, 0);

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.Goals.Single().Steps.Select(x => x.Id).Should().ContainInOrder(s2.Id, s1.Id);
    }

    [Fact]
    public void DeleteStep_WhenGoalHasMoreThanOne_ShouldRemove()
    {
        // Arrange
        var map = CreateMap();
        var goal = map.Goals.Single();
        var extraStep = map.AddStep(goal.Id, "Extra").Value;

        // Act
        var result = map.DeleteStep(extraStep.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.Goals.Single().Steps.Should().ContainSingle();
    }

    [Fact]
    public void DeleteStep_LastStepInGoal_ShouldSucceedAndLeaveGoalEmpty()
    {
        // Arrange
        var map = CreateMap();
        var onlyStepId = map.Goals.Single().Steps.Single().Id;

        // Act — a goal may be left with no steps.
        var result = map.DeleteStep(onlyStepId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.Goals.Single().Steps.Should().BeEmpty();
    }

    [Fact]
    public void DeleteStep_ShouldRemoveItsTasks()
    {
        // Arrange
        var map = CreateMap();
        var goal = map.Goals.Single();
        var stepToDelete = map.AddStep(goal.Id, "Doomed Step").Value;
        map.AddTask(stepToDelete.Id, "Task in doomed step");

        // Act
        var result = map.DeleteStep(stepToDelete.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.Goals.Single().Steps.Should().NotContain(s => s.Id == stepToDelete.Id);
        map.Goals.SelectMany(g => g.Steps).SelectMany(s => s.Tasks).Should().BeEmpty();
    }

    [Fact]
    public void DeleteStep_UnknownStep_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();

        // Act
        var result = map.DeleteStep(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region Tasks

    [Fact]
    public void AddTask_WithoutLane_ShouldLandInDefaultLane()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var defaultLaneId = map.SwimLanes.Single(l => l.IsDefault).Id;

        // Act
        var result = map.AddTask(stepId, "My Task");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("My Task");
        result.Value.SwimLaneId.Should().Be(defaultLaneId);
    }

    [Fact]
    public void AddTask_WithSpecificLane_ShouldLandInThatLane()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var lane = map.AddSwimLane("Release 1").Value;

        // Act
        var result = map.AddTask(stepId, "My Task", lane.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SwimLaneId.Should().Be(lane.Id);
    }

    [Fact]
    public void AddTask_WithoutLaneWhenOtherLanesExist_ShouldStillDefaultToDefaultLane()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var defaultLaneId = map.SwimLanes.Single(l => l.IsDefault).Id;
        map.AddSwimLane("Release 1");

        // Act
        var result = map.AddTask(stepId, "My Task");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SwimLaneId.Should().Be(defaultLaneId);
    }

    [Fact]
    public void AddTask_UnknownStep_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();

        // Act
        var result = map.AddTask(Guid.NewGuid(), "Task");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AddTask_UnknownLane_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;

        // Act
        var result = map.AddTask(stepId, "Task", Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void UpdateTask_ExistingTask_ShouldUpdateTitleAndDescription()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var task = map.AddTask(stepId, "Original").Value;

        // Act
        var result = map.UpdateTask(task.Id, "Updated", "Some description");

        // Assert
        result.IsSuccess.Should().BeTrue();
        task.Title.Should().Be("Updated");
        task.Description.Should().Be("Some description");
    }

    [Fact]
    public void UpdateTask_UnknownTask_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();

        // Act
        var result = map.UpdateTask(Guid.NewGuid(), "Title", null);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void MoveTask_AcrossStepsAndLanes_ShouldRelocateTask()
    {
        // Arrange
        var map = CreateMap();
        var goal = map.Goals.Single();
        var sourceStep = goal.Steps.Single();
        var targetStep = map.AddStep(goal.Id, "Target Step").Value;
        var targetLane = map.AddSwimLane("Lane 2").Value;
        var task = map.AddTask(sourceStep.Id, "Movable").Value;

        // Act
        var result = map.MoveTask(task.Id, targetStep.Id, targetLane.Id, 0);

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.Goals.Single().Steps.First(s => s.Id == sourceStep.Id).Tasks.Should().BeEmpty();
        var relocatedStep = map.Goals.Single().Steps.First(s => s.Id == targetStep.Id);
        relocatedStep.Tasks.Should().ContainSingle();
        relocatedStep.Tasks.Single().Id.Should().Be(task.Id);
        relocatedStep.Tasks.Single().SwimLaneId.Should().Be(targetLane.Id);
        relocatedStep.Tasks.Single().StepId.Should().Be(targetStep.Id);
    }

    [Fact]
    public void MoveTask_UnknownTask_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var swimLaneId = map.SwimLanes.Single().Id;

        // Act
        var result = map.MoveTask(Guid.NewGuid(), stepId, swimLaneId, 0);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void MoveTask_UnknownLane_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var task = map.AddTask(stepId, "Task").Value;

        // Act
        var result = map.MoveTask(task.Id, stepId, Guid.NewGuid(), 0);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void DeleteTask_ExistingTask_ShouldRemove()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var task = map.AddTask(stepId, "Task").Value;

        // Act
        var result = map.DeleteTask(task.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.Goals.Single().Steps.Single().Tasks.Should().BeEmpty();
    }

    [Fact]
    public void DeleteTask_UnknownTask_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();

        // Act
        var result = map.DeleteTask(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SetTaskPersonas_UnknownPersonaId_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var task = map.AddTask(stepId, "Task").Value;

        // Act
        var result = map.SetTaskPersonas(task.Id, [Guid.NewGuid()]);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SetTaskPersonas_KnownPersonaId_ShouldTagTask()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var task = map.AddTask(stepId, "Task").Value;
        var persona = map.AddPersona("Admin", null, "#4096FF").Value;

        // Act
        var result = map.SetTaskPersonas(task.Id, [persona.Id]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task.PersonaIds.Should().ContainSingle().Which.Should().Be(persona.Id);
    }

    [Fact]
    public void SetTaskPersonas_UnknownTask_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();

        // Act
        var result = map.SetTaskPersonas(Guid.NewGuid(), []);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region SwimLanes

    [Fact]
    public void AddLane_ShouldAppendBelowExistingLanes()
    {
        // Arrange
        var map = CreateMap();

        // Act
        var result = map.AddSwimLane("Release 1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Release 1");
        result.Value.IsDefault.Should().BeFalse();
        result.Value.Order.Should().Be(1);
        map.SwimLanes.Should().HaveCount(2);
        map.SwimLanes.Last().Id.Should().Be(result.Value.Id);
    }

    [Fact]
    public void RenameLane_NormalLane_ShouldRename()
    {
        // Arrange
        var map = CreateMap();
        var lane = map.AddSwimLane("Release 1").Value;

        // Act
        var result = map.RenameSwimLane(lane.Id, "Release 2");

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.SwimLanes.First(l => l.Id == lane.Id).Name.Should().Be("Release 2");
    }

    [Fact]
    public void RenameLane_DefaultLane_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();
        var defaultLaneId = map.SwimLanes.Single(l => l.IsDefault).Id;

        // Act
        var result = map.RenameSwimLane(defaultLaneId, "New Name");

        // Assert
        result.IsFailure.Should().BeTrue();
        map.SwimLanes.Single(l => l.IsDefault).Name.Should().Be("Tasks");
    }

    [Fact]
    public void RenameLane_UnknownLane_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();

        // Act
        var result = map.RenameSwimLane(Guid.NewGuid(), "Name");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ReorderLane_DefaultLane_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();
        var defaultLaneId = map.SwimLanes.Single(l => l.IsDefault).Id;

        // Act
        var result = map.ReorderSwimLane(defaultLaneId, 1);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ReorderLane_CannotPushLaneAboveDefault_DefaultStaysAtZero()
    {
        // Arrange
        var map = CreateMap();
        var lane1 = map.AddSwimLane("Lane 1").Value;
        var lane2 = map.AddSwimLane("Lane 2").Value;

        // Act
        var result = map.ReorderSwimLane(lane2.Id, 0);

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.SwimLanes.First().IsDefault.Should().BeTrue();
        map.SwimLanes.First().Order.Should().Be(0);
        // lane2 clamps to order 1 (just below default), lane1 follows.
        map.SwimLanes.Select(l => l.Id).Should().ContainInOrder(
            map.SwimLanes.Single(l => l.IsDefault).Id, lane2.Id, lane1.Id);
    }

    [Fact]
    public void RemoveLane_DefaultLane_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();
        var defaultLaneId = map.SwimLanes.Single(l => l.IsDefault).Id;

        // Act
        var result = map.RemoveSwimLane(defaultLaneId);

        // Assert
        result.IsFailure.Should().BeTrue();
        map.SwimLanes.Should().ContainSingle();
    }

    [Fact]
    public void RemoveLane_UnknownLane_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();

        // Act
        var result = map.RemoveSwimLane(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RemoveLane_WithTasks_ShouldReturnMovedCountAndReturnTasksToDefaultLane()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var lane = map.AddSwimLane("Release 1").Value;
        var defaultLaneId = map.SwimLanes.Single(l => l.IsDefault).Id;
        var task1 = map.AddTask(stepId, "Task 1", lane.Id).Value;
        var task2 = map.AddTask(stepId, "Task 2", lane.Id).Value;

        // Act
        var result = map.RemoveSwimLane(lane.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);
        map.SwimLanes.Should().ContainSingle();
        var tasks = map.Goals.Single().Steps.Single().Tasks;
        tasks.Should().HaveCount(2);
        tasks.Should().OnlyContain(t => t.SwimLaneId == defaultLaneId);
        tasks.Select(t => t.Id).Should().Contain([task1.Id, task2.Id]);
    }

    [Fact]
    public void SetLaneDates_ShouldSetStartAndEnd()
    {
        // Arrange
        var map = CreateMap();
        var lane = map.AddSwimLane("Release 1").Value;
        var start = new LocalDate(2026, 1, 1);
        var end = new LocalDate(2026, 3, 1);

        // Act
        var result = map.SetSwimLaneDates(lane.Id, start, end);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updated = map.SwimLanes.First(l => l.Id == lane.Id);
        updated.StartDate.Should().Be(start);
        updated.EndDate.Should().Be(end);
    }

    [Fact]
    public void SetLaneDates_EndBeforeStart_ShouldStillSucceed()
    {
        // Arrange
        var map = CreateMap();
        var lane = map.AddSwimLane("Release 1").Value;
        var start = new LocalDate(2026, 3, 1);
        var end = new LocalDate(2026, 1, 1);

        // Act
        var result = map.SetSwimLaneDates(lane.Id, start, end);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updated = map.SwimLanes.First(l => l.Id == lane.Id);
        updated.StartDate.Should().Be(start);
        updated.EndDate.Should().Be(end);
    }

    [Fact]
    public void SetLaneDates_UnknownLane_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();

        // Act
        var result = map.SetSwimLaneDates(Guid.NewGuid(), null, null);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region Personas

    [Fact]
    public void AddPersona_ValidParameters_ShouldAdd()
    {
        // Arrange
        var map = CreateMap();

        // Act
        var result = map.AddPersona("Admin", "An admin user", "#4096FF");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Admin");
        result.Value.Description.Should().Be("An admin user");
        result.Value.Color.Should().Be("#4096FF");
        map.Personas.Should().ContainSingle();
    }

    [Fact]
    public void UpdatePersona_ExistingPersona_ShouldUpdate()
    {
        // Arrange
        var map = CreateMap();
        var persona = map.AddPersona("Admin", null, "#4096FF").Value;

        // Act
        var result = map.UpdatePersona(persona.Id, "Super Admin", "desc", "#FF0000");

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updated = map.Personas.Single();
        updated.Name.Should().Be("Super Admin");
        updated.Description.Should().Be("desc");
        updated.Color.Should().Be("#FF0000");
    }

    [Fact]
    public void UpdatePersona_UnknownPersona_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();

        // Act
        var result = map.UpdatePersona(Guid.NewGuid(), "Name", null, "#000000");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void DeletePersona_TaggedOnMultipleNodes_ShouldReturnCountAndStripTags()
    {
        // Arrange
        var map = CreateMap();
        var goal = map.Goals.Single();
        var step = goal.Steps.Single();
        var task = map.AddTask(step.Id, "Task").Value;
        var persona = map.AddPersona("Admin", null, "#4096FF").Value;
        map.SetGoalPersonas(goal.Id, [persona.Id]);
        map.SetStepPersonas(step.Id, [persona.Id]);
        map.SetTaskPersonas(task.Id, [persona.Id]);

        // Act
        var result = map.DeletePersona(persona.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(3);
        map.Personas.Should().BeEmpty();
        map.Goals.Single().PersonaIds.Should().NotContain(persona.Id);
        map.Goals.Single().Steps.Single().PersonaIds.Should().NotContain(persona.Id);
        map.Goals.Single().Steps.Single().Tasks.Single().PersonaIds.Should().NotContain(persona.Id);
    }

    [Fact]
    public void DeletePersona_NotTagged_ShouldReturnZero()
    {
        // Arrange
        var map = CreateMap();
        var persona = map.AddPersona("Admin", null, "#4096FF").Value;

        // Act
        var result = map.DeletePersona(persona.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
        map.Personas.Should().BeEmpty();
    }

    [Fact]
    public void DeletePersona_UnknownPersona_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();

        // Act
        var result = map.DeletePersona(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void CountPersonaTags_ShouldReflectTaggedNodes()
    {
        // Arrange
        var map = CreateMap();
        var goal = map.Goals.Single();
        var step = goal.Steps.Single();
        var task = map.AddTask(step.Id, "Task").Value;
        var persona = map.AddPersona("Admin", null, "#4096FF").Value;
        map.SetGoalPersonas(goal.Id, [persona.Id]);
        map.SetTaskPersonas(task.Id, [persona.Id]);

        // Act
        var count = map.CountPersonaTags(persona.Id);

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public void SetGoalPersonas_UnknownPersonaId_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();
        var goalId = map.Goals.Single().Id;

        // Act
        var result = map.SetGoalPersonas(goalId, [Guid.NewGuid()]);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SetGoalPersonas_UnknownGoal_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();

        // Act
        var result = map.SetGoalPersonas(Guid.NewGuid(), []);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SetStepPersonas_UnknownPersonaId_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;

        // Act
        var result = map.SetStepPersonas(stepId, [Guid.NewGuid()]);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SetStepPersonas_KnownPersonaId_ShouldTagStep()
    {
        // Arrange
        var map = CreateMap();
        var step = map.Goals.Single().Steps.Single();
        var persona = map.AddPersona("Admin", null, "#4096FF").Value;

        // Act
        var result = map.SetStepPersonas(step.Id, [persona.Id]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.Goals.Single().Steps.Single().PersonaIds.Should().ContainSingle().Which.Should().Be(persona.Id);
    }

    [Fact]
    public void SetStepPersonas_UnknownStep_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();

        // Act
        var result = map.SetStepPersonas(Guid.NewGuid(), []);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region Checklist

    [Fact]
    public void AddChecklistItem_ExistingTask_ShouldAdd()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var task = map.AddTask(stepId, "Task").Value;

        // Act
        var result = map.AddChecklistItem(task.Id, "Item 1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Item 1");
        result.Value.IsChecked.Should().BeFalse();
        task.Checklist.Should().ContainSingle();
    }

    [Fact]
    public void AddChecklistItem_UnknownTask_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();

        // Act
        var result = map.AddChecklistItem(Guid.NewGuid(), "Item");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SetChecklistItemChecked_ShouldMarkChecked()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var task = map.AddTask(stepId, "Task").Value;
        var item = map.AddChecklistItem(task.Id, "Item 1").Value;

        // Act
        var result = map.SetChecklistItemChecked(task.Id, item.Id, true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task.Checklist.Single().IsChecked.Should().BeTrue();
    }

    [Fact]
    public void CompletionCount_ShouldReflectCheckedAndTotal()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var task = map.AddTask(stepId, "Task").Value;
        var i1 = map.AddChecklistItem(task.Id, "Item 1").Value;
        var i2 = map.AddChecklistItem(task.Id, "Item 2").Value;
        var i3 = map.AddChecklistItem(task.Id, "Item 3").Value;
        map.AddChecklistItem(task.Id, "Item 4");
        map.AddChecklistItem(task.Id, "Item 5");
        map.SetChecklistItemChecked(task.Id, i1.Id, true);
        map.SetChecklistItemChecked(task.Id, i2.Id, true);
        map.SetChecklistItemChecked(task.Id, i3.Id, true);

        // Act
        var (completed, total) = task.CompletionCount;

        // Assert
        completed.Should().Be(3);
        total.Should().Be(5);
    }

    [Fact]
    public void RenameChecklistItem_ShouldRename()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var task = map.AddTask(stepId, "Task").Value;
        var item = map.AddChecklistItem(task.Id, "Item 1").Value;

        // Act
        var result = map.RenameChecklistItem(task.Id, item.Id, "Renamed Item");

        // Assert
        result.IsSuccess.Should().BeTrue();
        task.Checklist.Single().Name.Should().Be("Renamed Item");
    }

    [Fact]
    public void RemoveChecklistItem_ShouldRemove()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var task = map.AddTask(stepId, "Task").Value;
        var item = map.AddChecklistItem(task.Id, "Item 1").Value;

        // Act
        var result = map.RemoveChecklistItem(task.Id, item.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task.Checklist.Should().BeEmpty();
    }

    [Fact]
    public void PromoteChecklistItem_ShouldRemoveItemAndCreateTaskInSameStepDefaultLane()
    {
        // Arrange
        var map = CreateMap();
        var step = map.Goals.Single().Steps.Single();
        var task = map.AddTask(step.Id, "Task").Value;
        var defaultLaneId = map.SwimLanes.Single(l => l.IsDefault).Id;
        var item = map.AddChecklistItem(task.Id, "Promote Me").Value;

        // Act
        var result = map.PromoteChecklistItem(task.Id, item.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Promote Me");
        result.Value.StepId.Should().Be(step.Id);
        result.Value.SwimLaneId.Should().Be(defaultLaneId);
        task.Checklist.Should().BeEmpty();
        map.Goals.Single().Steps.Single().Tasks.Should().Contain(t => t.Id == result.Value.Id);
    }

    [Fact]
    public void PromoteChecklistItem_UnknownItem_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var task = map.AddTask(stepId, "Task").Value;

        // Act
        var result = map.PromoteChecklistItem(task.Id, Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region Work item link

    [Fact]
    public void LinkWorkItem_ShouldSetLinkedWorkItemId()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var task = map.AddTask(stepId, "Task").Value;

        // Act
        var result = map.LinkWorkItem(task.Id, 42);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task.LinkedWorkItemId.Should().Be(42);
    }

    [Fact]
    public void UnlinkWorkItem_ShouldClearLinkedWorkItemId()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var task = map.AddTask(stepId, "Task").Value;
        map.LinkWorkItem(task.Id, 42);

        // Act
        var result = map.UnlinkWorkItem(task.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task.LinkedWorkItemId.Should().BeNull();
    }

    [Fact]
    public void LinkWorkItem_SameWorkItemToSecondTask_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var task1 = map.AddTask(stepId, "Task 1").Value;
        var task2 = map.AddTask(stepId, "Task 2").Value;
        map.LinkWorkItem(task1.Id, 42);

        // Act
        var result = map.LinkWorkItem(task2.Id, 42);

        // Assert
        result.IsFailure.Should().BeTrue();
        task2.LinkedWorkItemId.Should().BeNull();
    }

    [Fact]
    public void LinkWorkItem_UnknownTask_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();

        // Act
        var result = map.LinkWorkItem(Guid.NewGuid(), 42);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion
}
