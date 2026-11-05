using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models.StoryMaps;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Domain.Tests.Sut.Models.StoryMaps;

public class StoryMapTests
{
    // A map created the real way starts empty; seed a first goal with a step for the many tests that
    // need a graph to operate on.
    private static StoryMap CreateMap()
    {
        var map = StoryMap.Create("My Map", "A description", StoryMapActors.OwnerEmployeeId).Value;
        var goal = map.AddGoal("First Goal", StoryMapActors.Owner).Value;
        map.AddStep(goal.Id, "First Step", StoryMapActors.Owner);
        return map;
    }

    #region Create

    [Fact]
    public void Create_ValidParameters_ShouldReturnActiveMap()
    {
        // Arrange
        var ownerId = Guid.NewGuid();

        // Act
        var result = StoryMap.Create("My Map", "A description", ownerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var map = result.Value;
        map.Name.Should().Be("My Map");
        map.Description.Should().Be("A description");
        map.Owners.Should().ContainSingle().Which.EmployeeId.Should().Be(ownerId);
        map.Editors.Should().BeEmpty();
        map.EditorTeams.Should().BeEmpty();
        map.Visibility.Should().Be(Visibility.Public);
        map.EditAccess.Should().Be(StoryMapEditAccess.AnyoneWhoCanView);
        map.Status.Should().Be(StatusCategory.Active);
    }

    [Fact]
    public void Create_WithVisibilityAndEditAccess_ShouldKeepThem()
    {
        // Act
        var map = StoryMap.Create("My Map", null, Guid.NewGuid(), Visibility.Private, StoryMapEditAccess.OwnersAndEditors).Value;

        // Assert
        map.Visibility.Should().Be(Visibility.Private);
        map.EditAccess.Should().Be(StoryMapEditAccess.OwnersAndEditors);
    }

    [Fact]
    public void Create_EmptyOwner_ShouldThrow()
    {
        // Act
        var act = () => StoryMap.Create("My Map", null, Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ValidParameters_ShouldStartWithNoGoals()
    {
        // Act
        var map = StoryMap.Create("My Map", "A description", StoryMapActors.OwnerEmployeeId).Value;

        // Assert — a new map is empty; the user adds the first goal from the board's empty state.
        map.Goals.Should().BeEmpty();
    }

    [Fact]
    public void Create_ValidParameters_ShouldSeedSingleDefaultSwimLaneNamedTasks()
    {
        // Act
        var map = StoryMap.Create("My Map", "A description", StoryMapActors.OwnerEmployeeId).Value;

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
        var result = StoryMap.Create("   ", null, StoryMapActors.OwnerEmployeeId);

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
        var result = map.Update("New Name", "New description", StoryMapActors.Owner);

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
        var result = map.Update("   ", "desc", StoryMapActors.Owner);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Archive_ActiveMap_ShouldSetStatusToRemoved()
    {
        // Arrange
        var map = CreateMap();

        // Act
        var result = map.Archive(StoryMapActors.Owner);

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.Status.Should().Be(StatusCategory.Removed);
    }

    [Fact]
    public void Archive_AlreadyArchived_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();
        map.Archive(StoryMapActors.Owner);

        // Act
        var result = map.Archive(StoryMapActors.Owner);

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
        var result = map.AddGoal("Second Goal", StoryMapActors.Owner);

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
        var result = map.AddGoal("   ", StoryMapActors.Owner);

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
        var result = map.RenameGoal(goalId, "Renamed Goal", StoryMapActors.Owner);

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
        var result = map.RenameGoal(Guid.NewGuid(), "Name", StoryMapActors.Owner);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ReorderGoal_ShouldRenumberContiguously()
    {
        // Arrange
        var map = CreateMap();
        var g1 = map.Goals.Single();
        var g2 = map.AddGoal("Goal 2", StoryMapActors.Owner).Value;
        var g3 = map.AddGoal("Goal 3", StoryMapActors.Owner).Value;

        // Act
        var result = map.ReorderGoal(g3.Id, 0, StoryMapActors.Owner);

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
        var g2 = map.AddGoal("Goal 2", StoryMapActors.Owner).Value;

        // Act
        var result = map.DeleteGoal(g1.Id, StoryMapActors.Owner);

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
        var result = map.DeleteGoal(goalId, StoryMapActors.Owner);

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
        var result = map.DeleteGoal(Guid.NewGuid(), StoryMapActors.Owner);

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
        var result = map.AddStep(goal.Id, "Second Step", StoryMapActors.Owner);

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
        var result = map.AddStep(Guid.NewGuid(), "Step", StoryMapActors.Owner);

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
        var result = map.RenameStep(stepId, "Renamed Step", StoryMapActors.Owner);

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
        var result = map.RenameStep(Guid.NewGuid(), "Name", StoryMapActors.Owner);

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
        var s2 = map.AddStep(goal.Id, "Step 2", StoryMapActors.Owner).Value;
        var s3 = map.AddStep(goal.Id, "Step 3", StoryMapActors.Owner).Value;

        // Act
        var result = map.ReorderStep(s3.Id, 0, StoryMapActors.Owner);

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
        var extraStep = map.AddStep(sourceGoal.Id, "Extra Step", StoryMapActors.Owner).Value;
        var targetGoal = map.AddGoal("Target Goal", StoryMapActors.Owner).Value;

        // Act — target goal starts empty, so it holds just the moved step afterwards.
        var result = map.MoveStep(extraStep.Id, targetGoal.Id, 0, StoryMapActors.Owner);

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
        var targetGoal = map.AddGoal("Target Goal", StoryMapActors.Owner).Value;

        // Act — a goal may be left with no steps.
        var result = map.MoveStep(onlyStepId, targetGoal.Id, 0, StoryMapActors.Owner);

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
        var s2 = map.AddStep(goal.Id, "Step 2", StoryMapActors.Owner).Value;

        // Act
        var result = map.MoveStep(s2.Id, goal.Id, 0, StoryMapActors.Owner);

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
        var extraStep = map.AddStep(goal.Id, "Extra", StoryMapActors.Owner).Value;

        // Act
        var result = map.DeleteStep(extraStep.Id, StoryMapActors.Owner);

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
        var result = map.DeleteStep(onlyStepId, StoryMapActors.Owner);

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
        var stepToDelete = map.AddStep(goal.Id, "Doomed Step", StoryMapActors.Owner).Value;
        map.AddTask(stepToDelete.Id, "Task in doomed step", null, StoryMapActors.Owner);

        // Act
        var result = map.DeleteStep(stepToDelete.Id, StoryMapActors.Owner);

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
        var result = map.DeleteStep(Guid.NewGuid(), StoryMapActors.Owner);

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
        var result = map.AddTask(stepId, "My Task", null, StoryMapActors.Owner);

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
        var lane = map.AddSwimLane("Release 1", StoryMapActors.Owner).Value;

        // Act
        var result = map.AddTask(stepId, "My Task", lane.Id, StoryMapActors.Owner);

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
        map.AddSwimLane("Release 1", StoryMapActors.Owner);

        // Act
        var result = map.AddTask(stepId, "My Task", null, StoryMapActors.Owner);

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
        var result = map.AddTask(Guid.NewGuid(), "Task", null, StoryMapActors.Owner);

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
        var result = map.AddTask(stepId, "Task", Guid.NewGuid(), StoryMapActors.Owner);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void UpdateTask_ExistingTask_ShouldUpdateTitleAndDescription()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var task = map.AddTask(stepId, "Original", null, StoryMapActors.Owner).Value;

        // Act
        var result = map.UpdateTask(task.Id, "Updated", "Some description", StoryMapActors.Owner);

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
        var result = map.UpdateTask(Guid.NewGuid(), "Title", null, StoryMapActors.Owner);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RenameTask_ExistingTask_ShouldChangeTitleOnly()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var task = map.AddTask(stepId, "Original", null, StoryMapActors.Owner).Value;
        map.SetTaskDescription(task.Id, "Existing description", StoryMapActors.Owner);

        // Act
        var result = map.RenameTask(task.Id, "Updated", StoryMapActors.Owner);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task.Title.Should().Be("Updated");
        task.Description.Should().Be("Existing description");
    }

    [Fact]
    public void RenameTask_UnknownTask_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();

        // Act
        var result = map.RenameTask(Guid.NewGuid(), "Title", StoryMapActors.Owner);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SetTaskDescription_ExistingTask_ShouldChangeDescriptionOnly()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var task = map.AddTask(stepId, "Original", null, StoryMapActors.Owner).Value;

        // Act
        var result = map.SetTaskDescription(task.Id, "Some description", StoryMapActors.Owner);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task.Title.Should().Be("Original");
        task.Description.Should().Be("Some description");
    }

    [Fact]
    public void SetTaskDescription_Null_ShouldClearDescription()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var task = map.AddTask(stepId, "Original", null, StoryMapActors.Owner).Value;
        map.SetTaskDescription(task.Id, "Existing description", StoryMapActors.Owner);

        // Act
        var result = map.SetTaskDescription(task.Id, null, StoryMapActors.Owner);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task.Description.Should().BeNull();
    }

    [Fact]
    public void SetTaskDescription_UnknownTask_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();

        // Act
        var result = map.SetTaskDescription(Guid.NewGuid(), "Description", StoryMapActors.Owner);

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
        var targetStep = map.AddStep(goal.Id, "Target Step", StoryMapActors.Owner).Value;
        var targetLane = map.AddSwimLane("Lane 2", StoryMapActors.Owner).Value;
        var task = map.AddTask(sourceStep.Id, "Movable", null, StoryMapActors.Owner).Value;

        // Act
        var result = map.MoveTask(task.Id, targetStep.Id, targetLane.Id, 0, StoryMapActors.Owner);

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
        var result = map.MoveTask(Guid.NewGuid(), stepId, swimLaneId, 0, StoryMapActors.Owner);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void MoveTask_UnknownLane_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var task = map.AddTask(stepId, "Task", null, StoryMapActors.Owner).Value;

        // Act
        var result = map.MoveTask(task.Id, stepId, Guid.NewGuid(), 0, StoryMapActors.Owner);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void DeleteTask_ExistingTask_ShouldRemove()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var task = map.AddTask(stepId, "Task", null, StoryMapActors.Owner).Value;

        // Act
        var result = map.DeleteTask(task.Id, StoryMapActors.Owner);

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
        var result = map.DeleteTask(Guid.NewGuid(), StoryMapActors.Owner);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SetTaskPersonas_UnknownPersonaId_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var task = map.AddTask(stepId, "Task", null, StoryMapActors.Owner).Value;

        // Act
        var result = map.SetTaskPersonas(task.Id, [Guid.NewGuid()], StoryMapActors.Owner);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SetTaskPersonas_KnownPersonaId_ShouldTagTask()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var task = map.AddTask(stepId, "Task", null, StoryMapActors.Owner).Value;
        var persona = map.AddPersona("Admin", null, "#4096FF", StoryMapActors.Owner).Value;

        // Act
        var result = map.SetTaskPersonas(task.Id, [persona.Id], StoryMapActors.Owner);

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
        var result = map.SetTaskPersonas(Guid.NewGuid(), [], StoryMapActors.Owner);

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
        var result = map.AddSwimLane("Release 1", StoryMapActors.Owner);

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
        var lane = map.AddSwimLane("Release 1", StoryMapActors.Owner).Value;

        // Act
        var result = map.RenameSwimLane(lane.Id, "Release 2", StoryMapActors.Owner);

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
        var result = map.RenameSwimLane(defaultLaneId, "New Name", StoryMapActors.Owner);

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
        var result = map.RenameSwimLane(Guid.NewGuid(), "Name", StoryMapActors.Owner);

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
        var result = map.ReorderSwimLane(defaultLaneId, 1, StoryMapActors.Owner);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ReorderLane_CannotPushLaneAboveDefault_DefaultStaysAtZero()
    {
        // Arrange
        var map = CreateMap();
        var lane1 = map.AddSwimLane("Lane 1", StoryMapActors.Owner).Value;
        var lane2 = map.AddSwimLane("Lane 2", StoryMapActors.Owner).Value;

        // Act
        var result = map.ReorderSwimLane(lane2.Id, 0, StoryMapActors.Owner);

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
        var result = map.RemoveSwimLane(defaultLaneId, StoryMapActors.Owner);

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
        var result = map.RemoveSwimLane(Guid.NewGuid(), StoryMapActors.Owner);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RemoveLane_WithTasks_ShouldReturnMovedCountAndReturnTasksToDefaultLane()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var lane = map.AddSwimLane("Release 1", StoryMapActors.Owner).Value;
        var defaultLaneId = map.SwimLanes.Single(l => l.IsDefault).Id;
        var task1 = map.AddTask(stepId, "Task 1", lane.Id, StoryMapActors.Owner).Value;
        var task2 = map.AddTask(stepId, "Task 2", lane.Id, StoryMapActors.Owner).Value;

        // Act
        var result = map.RemoveSwimLane(lane.Id, StoryMapActors.Owner);

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
    public void RemoveLane_WithTasksInDefaultLane_ShouldAppendMovedTasksAfterThem()
    {
        // Arrange — the default lane already holds a task, so the reassigned ones must queue behind
        // it rather than colliding on the same order values.
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var defaultLaneId = map.SwimLanes.Single(l => l.IsDefault).Id;
        var lane = map.AddSwimLane("Release 1", StoryMapActors.Owner).Value;

        var existing = map.AddTask(stepId, "Already here", defaultLaneId, StoryMapActors.Owner).Value;
        var moved1 = map.AddTask(stepId, "Moved first", lane.Id, StoryMapActors.Owner).Value;
        var moved2 = map.AddTask(stepId, "Moved second", lane.Id, StoryMapActors.Owner).Value;

        // Act
        var result = map.RemoveSwimLane(lane.Id, StoryMapActors.Owner);

        // Assert — contiguous from 0, with the incoming tasks keeping their relative order.
        result.IsSuccess.Should().BeTrue();
        var ordered = map.Goals.Single().Steps.Single().Tasks
            .Where(t => t.SwimLaneId == defaultLaneId)
            .OrderBy(t => t.Order)
            .ToList();
        ordered.Select(t => t.Id).Should().Equal(existing.Id, moved1.Id, moved2.Id);
        ordered.Select(t => t.Order).Should().Equal(0, 1, 2);
    }

    [Fact]
    public void RemoveLane_OnAMapWithNoDefaultLane_ShouldReturnFailure()
    {
        // Arrange — Generate() bypasses the Create factory, so there is no default lane to move
        // tasks to. This must fail rather than throw past the Result boundary.
        var map = new StoryMapFaker().Generate();

        // Act
        var result = map.RemoveSwimLane(Guid.NewGuid(), StoryMapActors.Owner);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SetLaneDates_ShouldSetStartAndEnd()
    {
        // Arrange
        var map = CreateMap();
        var lane = map.AddSwimLane("Release 1", StoryMapActors.Owner).Value;
        var start = new LocalDate(2026, 1, 1);
        var end = new LocalDate(2026, 3, 1);

        // Act
        var result = map.SetSwimLaneDates(lane.Id, start, end, StoryMapActors.Owner);

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
        var lane = map.AddSwimLane("Release 1", StoryMapActors.Owner).Value;
        var start = new LocalDate(2026, 3, 1);
        var end = new LocalDate(2026, 1, 1);

        // Act
        var result = map.SetSwimLaneDates(lane.Id, start, end, StoryMapActors.Owner);

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
        var result = map.SetSwimLaneDates(Guid.NewGuid(), null, null, StoryMapActors.Owner);

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
        var result = map.AddPersona("Admin", "An admin user", "#4096FF", StoryMapActors.Owner);

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
        var persona = map.AddPersona("Admin", null, "#4096FF", StoryMapActors.Owner).Value;

        // Act
        var result = map.UpdatePersona(persona.Id, "Super Admin", "desc", "#FF0000", StoryMapActors.Owner);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updated = map.Personas.Single();
        updated.Name.Should().Be("Super Admin");
        updated.Description.Should().Be("desc");
        updated.Color.Should().Be("#FF0000");
    }

    #region Task ordering

    [Fact]
    public void DeleteTask_ShouldRenumberTheRestOfTheCell()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var first = map.AddTask(stepId, "First", null, StoryMapActors.Owner).Value;
        var second = map.AddTask(stepId, "Second", null, StoryMapActors.Owner).Value;
        var third = map.AddTask(stepId, "Third", null, StoryMapActors.Owner).Value;

        // Act
        map.DeleteTask(second.Id, StoryMapActors.Owner).IsSuccess.Should().BeTrue();

        // Assert — contiguous from zero, like every other ordered collection on the map.
        var remaining = map.Goals.Single().Steps.Single().Tasks;
        remaining.Select(t => t.Id).Should().Equal(first.Id, third.Id);
        remaining.Select(t => t.Order).Should().Equal(0, 1);
    }

    [Fact]
    public void DeleteTask_ShouldOnlyRenumberItsOwnCell()
    {
        // Arrange — two lanes, so the untouched lane must keep its numbering.
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var lane = map.AddSwimLane("Release 1", StoryMapActors.Owner).Value;
        var defaultFirst = map.AddTask(stepId, "Default first", null, StoryMapActors.Owner).Value;
        var defaultSecond = map.AddTask(stepId, "Default second", null, StoryMapActors.Owner).Value;
        var otherLaneTask = map.AddTask(stepId, "Other lane", lane.Id, StoryMapActors.Owner).Value;

        // Act
        map.DeleteTask(defaultFirst.Id, StoryMapActors.Owner).IsSuccess.Should().BeTrue();

        // Assert
        var tasks = map.Goals.Single().Steps.Single().Tasks;
        tasks.Single(t => t.Id == defaultSecond.Id).Order.Should().Be(0);
        tasks.Single(t => t.Id == otherLaneTask.Id).Order.Should().Be(0);
    }

    [Fact]
    public void MoveTask_ToAnotherCell_ShouldRenumberTheCellItLeft()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var lane = map.AddSwimLane("Release 1", StoryMapActors.Owner).Value;
        var first = map.AddTask(stepId, "First", null, StoryMapActors.Owner).Value;
        var second = map.AddTask(stepId, "Second", null, StoryMapActors.Owner).Value;
        var third = map.AddTask(stepId, "Third", null, StoryMapActors.Owner).Value;

        // Act — take the middle task out of the default lane.
        map.MoveTask(second.Id, stepId, lane.Id, 0, StoryMapActors.Owner).IsSuccess.Should().BeTrue();

        // Assert — the gap it left is closed.
        var tasks = map.Goals.Single().Steps.Single().Tasks;
        tasks.Single(t => t.Id == first.Id).Order.Should().Be(0);
        tasks.Single(t => t.Id == third.Id).Order.Should().Be(1);
        tasks.Single(t => t.Id == second.Id).Order.Should().Be(0);
    }

    [Fact]
    public void MoveTask_IntoTheMiddleOfATargetCell_ShouldInsertAtThatPosition()
    {
        // Arrange — every existing move test targets position 0 of an empty cell.
        var map = CreateMap();
        var goal = map.Goals.Single();
        var sourceStep = goal.Steps.Single();
        var targetStep = map.AddStep(goal.Id, "Target step", StoryMapActors.Owner).Value;
        var defaultLaneId = map.SwimLanes.Single(l => l.IsDefault).Id;

        var existingFirst = map.AddTask(targetStep.Id, "Existing first", null, StoryMapActors.Owner).Value;
        var existingSecond = map.AddTask(targetStep.Id, "Existing second", null, StoryMapActors.Owner).Value;
        var existingThird = map.AddTask(targetStep.Id, "Existing third", null, StoryMapActors.Owner).Value;
        var moving = map.AddTask(sourceStep.Id, "Moving", null, StoryMapActors.Owner).Value;

        // Act — land between the first and second existing tasks.
        var result = map.MoveTask(moving.Id, targetStep.Id, defaultLaneId, 1, StoryMapActors.Owner);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var cell = map.Goals.Single().Steps
            .Single(s => s.Id == targetStep.Id)
            .Tasks
            .Where(t => t.SwimLaneId == defaultLaneId)
            .OrderBy(t => t.Order)
            .ToList();
        cell.Select(t => t.Id).Should().Equal(existingFirst.Id, moving.Id, existingSecond.Id, existingThird.Id);
        cell.Select(t => t.Order).Should().Equal(0, 1, 2, 3);
    }

    [Fact]
    public void MoveTask_WithAnOrderPastTheEnd_ShouldClampToTheEnd()
    {
        // Arrange
        var map = CreateMap();
        var goal = map.Goals.Single();
        var sourceStep = goal.Steps.Single();
        var targetStep = map.AddStep(goal.Id, "Target step", StoryMapActors.Owner).Value;
        var defaultLaneId = map.SwimLanes.Single(l => l.IsDefault).Id;

        var existing = map.AddTask(targetStep.Id, "Existing", null, StoryMapActors.Owner).Value;
        var moving = map.AddTask(sourceStep.Id, "Moving", null, StoryMapActors.Owner).Value;

        // Act
        var result = map.MoveTask(moving.Id, targetStep.Id, defaultLaneId, 99, StoryMapActors.Owner);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var cell = map.Goals.Single().Steps
            .Single(s => s.Id == targetStep.Id)
            .Tasks
            .OrderBy(t => t.Order)
            .ToList();
        cell.Select(t => t.Id).Should().Equal(existing.Id, moving.Id);
        cell.Select(t => t.Order).Should().Equal(0, 1);
    }

    [Fact]
    public void Tasks_ShouldGroupByLaneAndOrderWithinEach()
    {
        // Arrange — Order is scoped per (step × lane) cell, so ordering by Order alone would
        // interleave the lanes into a sequence matching neither.
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var lane = map.AddSwimLane("Release 1", StoryMapActors.Owner).Value;

        map.AddTask(stepId, "Default 0", null, StoryMapActors.Owner);
        map.AddTask(stepId, "Default 1", null, StoryMapActors.Owner);
        map.AddTask(stepId, "Other 0", lane.Id, StoryMapActors.Owner);
        map.AddTask(stepId, "Other 1", lane.Id, StoryMapActors.Owner);

        // Act
        var tasks = map.Goals.Single().Steps.Single().Tasks;

        // Assert — each lane's tasks are contiguous and internally ordered.
        foreach (var group in tasks.GroupBy(t => t.SwimLaneId))
        {
            group.Select(t => t.Order).Should().BeInAscendingOrder();
        }

        tasks.Select(t => t.SwimLaneId).Distinct().Should().HaveCount(2);
        tasks.Should().HaveCount(4);
    }

    #endregion Task ordering

    #region Archived maps are read-only

    private static StoryMap CreateArchivedMap()
    {
        var map = CreateMap();
        map.Archive(StoryMapActors.Owner).IsSuccess.Should().BeTrue();
        return map;
    }

    public static TheoryData<string, Func<StoryMap, Result>> ArchivedMutations()
    {
        // Every public mutation, exercised against an archived map. Ids that do not resolve are
        // fine — the archived guard runs before any lookup, which is exactly what is being asserted.
        var missing = Guid.NewGuid();
        return new TheoryData<string, Func<StoryMap, Result>>
        {
            { "Update", m => m.Update("New name", null, StoryMapActors.Owner) },
            { "AddGoal", m => m.AddGoal("Goal", StoryMapActors.Owner) },
            { "RenameGoal", m => m.RenameGoal(missing, "Goal", StoryMapActors.Owner) },
            { "ReorderGoal", m => m.ReorderGoal(missing, 0, StoryMapActors.Owner) },
            { "DeleteGoal", m => m.DeleteGoal(missing, StoryMapActors.Owner) },
            { "AddStep", m => m.AddStep(missing, "Step", StoryMapActors.Owner) },
            { "RenameStep", m => m.RenameStep(missing, "Step", StoryMapActors.Owner) },
            { "ReorderStep", m => m.ReorderStep(missing, 0, StoryMapActors.Owner) },
            { "MoveStep", m => m.MoveStep(missing, missing, 0, StoryMapActors.Owner) },
            { "DeleteStep", m => m.DeleteStep(missing, StoryMapActors.Owner) },
            { "AddTask", m => m.AddTask(missing, "Task", null, StoryMapActors.Owner) },
            { "UpdateTask", m => m.UpdateTask(missing, "Task", null, StoryMapActors.Owner) },
            { "RenameTask", m => m.RenameTask(missing, "Task", StoryMapActors.Owner) },
            { "SetTaskDescription", m => m.SetTaskDescription(missing, null, StoryMapActors.Owner) },
            { "MoveTask", m => m.MoveTask(missing, missing, missing, 0, StoryMapActors.Owner) },
            { "DeleteTask", m => m.DeleteTask(missing, StoryMapActors.Owner) },
            { "SetTaskPersonas", m => m.SetTaskPersonas(missing, [], StoryMapActors.Owner) },
            { "AddChecklistItem", m => m.AddChecklistItem(missing, "Item", StoryMapActors.Owner) },
            { "RenameChecklistItem", m => m.RenameChecklistItem(missing, missing, "Item", StoryMapActors.Owner) },
            { "SetChecklistItemChecked", m => m.SetChecklistItemChecked(missing, missing, true, StoryMapActors.Owner) },
            { "RemoveChecklistItem", m => m.RemoveChecklistItem(missing, missing, StoryMapActors.Owner) },
            { "PromoteChecklistItem", m => m.PromoteChecklistItem(missing, missing, StoryMapActors.Owner) },
            { "LinkWorkItem", m => m.LinkWorkItem(missing, 1, StoryMapActors.Owner) },
            { "UnlinkWorkItem", m => m.UnlinkWorkItem(missing, StoryMapActors.Owner) },
            { "AddSwimLane", m => m.AddSwimLane("Lane", StoryMapActors.Owner) },
            { "RenameSwimLane", m => m.RenameSwimLane(missing, "Lane", StoryMapActors.Owner) },
            { "SetSwimLaneDates", m => m.SetSwimLaneDates(missing, null, null, StoryMapActors.Owner) },
            { "ReorderSwimLane", m => m.ReorderSwimLane(missing, 1, StoryMapActors.Owner) },
            { "RemoveSwimLane", m => m.RemoveSwimLane(missing, StoryMapActors.Owner) },
            { "AddPersona", m => m.AddPersona("Persona", null, "#4096FF", StoryMapActors.Owner) },
            { "UpdatePersona", m => m.UpdatePersona(missing, "Persona", null, "#4096FF", StoryMapActors.Owner) },
            { "ReorderPersona", m => m.ReorderPersona(missing, 0, StoryMapActors.Owner) },
            { "DeletePersona", m => m.DeletePersona(missing, StoryMapActors.Owner) },
            { "SetGoalPersonas", m => m.SetGoalPersonas(missing, [], StoryMapActors.Owner) },
            { "SetStepPersonas", m => m.SetStepPersonas(missing, [], StoryMapActors.Owner) },
        };
    }

    [Theory]
    [MemberData(nameof(ArchivedMutations))]
    public void Mutation_OnAnArchivedMap_ShouldReturnFailure(string name, Func<StoryMap, Result> mutate)
    {
        // Arrange — the UI hides these, but the API is reachable directly.
        var map = CreateArchivedMap();

        // Act
        var result = mutate(map);

        // Assert
        result.IsFailure.Should().BeTrue($"{name} must be rejected on an archived map");
        result.Error.Should().Be("This story map is archived and cannot be changed.");
    }

    [Fact]
    public void Mutation_OnAnArchivedMap_ShouldNotChangeAnything()
    {
        // Arrange
        var map = CreateMap();
        var originalName = map.Name;
        var goalCount = map.Goals.Count;
        map.Archive(StoryMapActors.Owner);

        // Act
        map.Update("Renamed", "New description", StoryMapActors.Owner);
        map.AddGoal("Should not appear", StoryMapActors.Owner);

        // Assert
        map.Name.Should().Be(originalName);
        map.Goals.Should().HaveCount(goalCount);
    }

    [Fact]
    public void Archive_OnAnAlreadyArchivedMap_ShouldReturnItsOwnFailure()
    {
        // Arrange — archiving is the one lifecycle change with its own status rule, so it keeps its
        // own message rather than the generic archived one.
        var map = CreateArchivedMap();

        // Act
        var result = map.Archive(StoryMapActors.Owner);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only active story maps can be archived.");
    }

    #endregion Archived maps are read-only

    [Fact]
    public void UpdatePersona_BlankColor_ShouldNotApplyTheName()
    {
        // Arrange
        var map = CreateMap();
        var persona = map.AddPersona("Admin", "An admin user", "#4096FF", StoryMapActors.Owner).Value;

        // Act — the colour is rejected, so the whole update must be rejected.
        var result = map.UpdatePersona(persona.Id, "Super Admin", "new desc", "  ", StoryMapActors.Owner);

        // Assert — a failed call leaving the persona renamed would be a silent partial write.
        result.IsFailure.Should().BeTrue();
        var unchanged = map.Personas.Single();
        unchanged.Name.Should().Be("Admin");
        unchanged.Description.Should().Be("An admin user");
        unchanged.Color.Should().Be("#4096FF");
    }

    [Fact]
    public void UpdatePersona_BlankName_ShouldNotApplyAnything()
    {
        // Arrange
        var map = CreateMap();
        var persona = map.AddPersona("Admin", "An admin user", "#4096FF", StoryMapActors.Owner).Value;

        // Act
        var result = map.UpdatePersona(persona.Id, "  ", "new desc", "#FF0000", StoryMapActors.Owner);

        // Assert
        result.IsFailure.Should().BeTrue();
        var unchanged = map.Personas.Single();
        unchanged.Name.Should().Be("Admin");
        unchanged.Description.Should().Be("An admin user");
        unchanged.Color.Should().Be("#4096FF");
    }

    [Fact]
    public void UpdatePersona_UnknownPersona_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();

        // Act
        var result = map.UpdatePersona(Guid.NewGuid(), "Name", null, "#000000", StoryMapActors.Owner);

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
        var task = map.AddTask(step.Id, "Task", null, StoryMapActors.Owner).Value;
        var persona = map.AddPersona("Admin", null, "#4096FF", StoryMapActors.Owner).Value;
        map.SetGoalPersonas(goal.Id, [persona.Id], StoryMapActors.Owner);
        map.SetStepPersonas(step.Id, [persona.Id], StoryMapActors.Owner);
        map.SetTaskPersonas(task.Id, [persona.Id], StoryMapActors.Owner);

        // Act
        var result = map.DeletePersona(persona.Id, StoryMapActors.Owner);

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
        var persona = map.AddPersona("Admin", null, "#4096FF", StoryMapActors.Owner).Value;

        // Act
        var result = map.DeletePersona(persona.Id, StoryMapActors.Owner);

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
        var result = map.DeletePersona(Guid.NewGuid(), StoryMapActors.Owner);

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
        var task = map.AddTask(step.Id, "Task", null, StoryMapActors.Owner).Value;
        var persona = map.AddPersona("Admin", null, "#4096FF", StoryMapActors.Owner).Value;
        map.SetGoalPersonas(goal.Id, [persona.Id], StoryMapActors.Owner);
        map.SetTaskPersonas(task.Id, [persona.Id], StoryMapActors.Owner);

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
        var result = map.SetGoalPersonas(goalId, [Guid.NewGuid()], StoryMapActors.Owner);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SetGoalPersonas_UnknownGoal_ShouldReturnFailure()
    {
        // Arrange
        var map = CreateMap();

        // Act
        var result = map.SetGoalPersonas(Guid.NewGuid(), [], StoryMapActors.Owner);

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
        var result = map.SetStepPersonas(stepId, [Guid.NewGuid()], StoryMapActors.Owner);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SetStepPersonas_KnownPersonaId_ShouldTagStep()
    {
        // Arrange
        var map = CreateMap();
        var step = map.Goals.Single().Steps.Single();
        var persona = map.AddPersona("Admin", null, "#4096FF", StoryMapActors.Owner).Value;

        // Act
        var result = map.SetStepPersonas(step.Id, [persona.Id], StoryMapActors.Owner);

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
        var result = map.SetStepPersonas(Guid.NewGuid(), [], StoryMapActors.Owner);

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
        var task = map.AddTask(stepId, "Task", null, StoryMapActors.Owner).Value;

        // Act
        var result = map.AddChecklistItem(task.Id, "Item 1", StoryMapActors.Owner);

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
        var result = map.AddChecklistItem(Guid.NewGuid(), "Item", StoryMapActors.Owner);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SetChecklistItemChecked_ShouldMarkChecked()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var task = map.AddTask(stepId, "Task", null, StoryMapActors.Owner).Value;
        var item = map.AddChecklistItem(task.Id, "Item 1", StoryMapActors.Owner).Value;

        // Act
        var result = map.SetChecklistItemChecked(task.Id, item.Id, true, StoryMapActors.Owner);

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
        var task = map.AddTask(stepId, "Task", null, StoryMapActors.Owner).Value;
        var i1 = map.AddChecklistItem(task.Id, "Item 1", StoryMapActors.Owner).Value;
        var i2 = map.AddChecklistItem(task.Id, "Item 2", StoryMapActors.Owner).Value;
        var i3 = map.AddChecklistItem(task.Id, "Item 3", StoryMapActors.Owner).Value;
        map.AddChecklistItem(task.Id, "Item 4", StoryMapActors.Owner);
        map.AddChecklistItem(task.Id, "Item 5", StoryMapActors.Owner);
        map.SetChecklistItemChecked(task.Id, i1.Id, true, StoryMapActors.Owner);
        map.SetChecklistItemChecked(task.Id, i2.Id, true, StoryMapActors.Owner);
        map.SetChecklistItemChecked(task.Id, i3.Id, true, StoryMapActors.Owner);

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
        var task = map.AddTask(stepId, "Task", null, StoryMapActors.Owner).Value;
        var item = map.AddChecklistItem(task.Id, "Item 1", StoryMapActors.Owner).Value;

        // Act
        var result = map.RenameChecklistItem(task.Id, item.Id, "Renamed Item", StoryMapActors.Owner);

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
        var task = map.AddTask(stepId, "Task", null, StoryMapActors.Owner).Value;
        var item = map.AddChecklistItem(task.Id, "Item 1", StoryMapActors.Owner).Value;

        // Act
        var result = map.RemoveChecklistItem(task.Id, item.Id, StoryMapActors.Owner);

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
        var task = map.AddTask(step.Id, "Task", null, StoryMapActors.Owner).Value;
        var defaultLaneId = map.SwimLanes.Single(l => l.IsDefault).Id;
        var item = map.AddChecklistItem(task.Id, "Promote Me", StoryMapActors.Owner).Value;

        // Act
        var result = map.PromoteChecklistItem(task.Id, item.Id, StoryMapActors.Owner);

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
        var task = map.AddTask(stepId, "Task", null, StoryMapActors.Owner).Value;

        // Act
        var result = map.PromoteChecklistItem(task.Id, Guid.NewGuid(), StoryMapActors.Owner);

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
        var task = map.AddTask(stepId, "Task", null, StoryMapActors.Owner).Value;

        // Act
        var result = map.LinkWorkItem(task.Id, 42, StoryMapActors.Owner);

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
        var task = map.AddTask(stepId, "Task", null, StoryMapActors.Owner).Value;
        map.LinkWorkItem(task.Id, 42, StoryMapActors.Owner);

        // Act
        var result = map.UnlinkWorkItem(task.Id, StoryMapActors.Owner);

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
        var task1 = map.AddTask(stepId, "Task 1", null, StoryMapActors.Owner).Value;
        var task2 = map.AddTask(stepId, "Task 2", null, StoryMapActors.Owner).Value;
        map.LinkWorkItem(task1.Id, 42, StoryMapActors.Owner);

        // Act
        var result = map.LinkWorkItem(task2.Id, 42, StoryMapActors.Owner);

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
        var result = map.LinkWorkItem(Guid.NewGuid(), 42, StoryMapActors.Owner);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region Access

    private static readonly Guid EditorEmployeeId = Guid.Parse("5e0a0c00-0000-4000-a000-00000000e001");
    private static readonly Guid EditorTeamId = Guid.Parse("5e0a0c00-0000-4000-a000-00000000e0e0");

    private static StoryMap AccessMap(Visibility visibility, StoryMapEditAccess editAccess) =>
        new StoryMapFaker()
            .WithVisibility(visibility)
            .WithEditAccess(editAccess)
            .WithEditors(EditorEmployeeId)
            .WithEditorTeams(EditorTeamId)
            .Generate();

    public static TheoryData<string, Visibility, StoryMapEditAccess, bool, bool, bool> AccessCases()
    {
        // Actor, visibility, edit access, then CanAccess, CanEdit, CanManage.
        var data = new TheoryData<string, Visibility, StoryMapEditAccess, bool, bool, bool>();
        foreach (var editAccess in new[] { StoryMapEditAccess.AnyoneWhoCanView, StoryMapEditAccess.OwnersAndEditors })
        {
            data.Add("owner", Visibility.Public, editAccess, true, true, true);
            data.Add("owner", Visibility.Private, editAccess, true, true, true);
            data.Add("administrator", Visibility.Private, editAccess, true, true, true);
            data.Add("editor", Visibility.Public, editAccess, true, true, false);
            data.Add("editor", Visibility.Private, editAccess, true, true, false);
            data.Add("editor team member", Visibility.Private, editAccess, true, true, false);
            data.Add("viewer", Visibility.Private, editAccess, false, false, false);
        }

        data.Add("viewer", Visibility.Public, StoryMapEditAccess.AnyoneWhoCanView, true, true, false);
        data.Add("viewer", Visibility.Public, StoryMapEditAccess.OwnersAndEditors, true, false, false);
        return data;
    }

    private static StoryMapActor ActorFor(string name) => name switch
    {
        "owner" => StoryMapActors.Owner,
        "administrator" => StoryMapActors.Administrator,
        "editor" => StoryMapActors.Employee(EditorEmployeeId),
        "editor team member" => StoryMapActors.Employee(Guid.NewGuid(), EditorTeamId),
        _ => StoryMapActors.Viewer,
    };

    [Theory]
    [MemberData(nameof(AccessCases))]
    public void Access_ShouldFollowRoleVisibilityAndEditAccess(string actorName, Visibility visibility, StoryMapEditAccess editAccess, bool canAccess, bool canEdit, bool canManage)
    {
        // Arrange
        var map = AccessMap(visibility, editAccess);
        var actor = ActorFor(actorName);

        // Act
        var access = (map.CanAccess(actor), map.CanEdit(actor), map.CanManage(actor));
        var accessible = StoryMap.AccessibleTo(actor.EmployeeId, actor.IsAdministrator, [.. actor.TeamIds]).Compile()(map);
        var editable = StoryMap.EditableBy(actor.EmployeeId, actor.IsAdministrator, [.. actor.TeamIds]).Compile()(map);

        // Assert — the query filters must agree with the methods they mirror.
        access.Should().Be((canAccess, canEdit, canManage));
        accessible.Should().Be(canAccess);
        editable.Should().Be(canEdit);
    }

    [Fact]
    public void AccessibleTo_ReaderWithNoEmployee_ShouldSeeOnlyPublicMaps()
    {
        // Arrange
        var publicMap = AccessMap(Visibility.Public, StoryMapEditAccess.AnyoneWhoCanView);
        var privateMap = AccessMap(Visibility.Private, StoryMapEditAccess.AnyoneWhoCanView);

        // Act
        var filter = StoryMap.AccessibleTo(null, false, []).Compile();

        // Assert
        filter(publicMap).Should().BeTrue();
        filter(privateMap).Should().BeFalse();
    }

    [Fact]
    public void AddGoal_ByViewerOnOwnersAndEditorsMap_ShouldFailAsNotAnEditor()
    {
        // Arrange
        var map = AccessMap(Visibility.Public, StoryMapEditAccess.OwnersAndEditors);

        // Act
        var result = map.AddGoal("Goal", StoryMapActors.Viewer);

        // Assert
        result.Error.Should().Be(StoryMap.NotAnEditorError);
        map.Goals.Should().BeEmpty();
    }

    [Fact]
    public void AddGoal_ByOutsiderOnPrivateMap_ShouldFailAsNotFound()
    {
        // Arrange
        var map = AccessMap(Visibility.Private, StoryMapEditAccess.AnyoneWhoCanView);

        // Act
        var result = map.AddGoal("Goal", StoryMapActors.Viewer);

        // Assert
        result.Error.Should().Be(StoryMap.NotFoundError);
    }

    [Fact]
    public void AddGoal_ByEditorTeamMemberOnPrivateMap_ShouldSucceed()
    {
        // Arrange
        var map = AccessMap(Visibility.Private, StoryMapEditAccess.OwnersAndEditors);

        // Act
        var result = map.AddGoal("Goal", StoryMapActors.Employee(Guid.NewGuid(), EditorTeamId));

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void AddGoal_ByOutsiderOnArchivedPrivateMap_ShouldFailAsNotFoundRatherThanArchived()
    {
        // Arrange
        var map = new StoryMapFaker().WithVisibility(Visibility.Private).WithStatus(StatusCategory.Removed).Generate();

        // Act
        var result = map.AddGoal("Goal", StoryMapActors.Viewer);

        // Assert
        result.Error.Should().Be(StoryMap.NotFoundError);
    }

    public static TheoryData<string, Func<StoryMap, StoryMapActor, Result>> ManageActions() => new()
    {
        { "Update", (m, a) => m.Update("Renamed", null, a) },
        { "Archive", (m, a) => m.Archive(a) },
        { "CanDelete", (m, a) => m.CanDelete(a) },
        { "SetVisibility", (m, a) => m.SetVisibility(Visibility.Private, a) },
        { "SetEditAccess", (m, a) => m.SetEditAccess(StoryMapEditAccess.OwnersAndEditors, a) },
        { "SetOwners", (m, a) => m.SetOwners([a.EmployeeId], a) },
        { "SetEditors", (m, a) => m.SetEditors([], [], a) },
    };

    [Theory]
    [MemberData(nameof(ManageActions))]
    public void ManageAction_ByEditor_ShouldFailAsNotAnOwner(string name, Func<StoryMap, StoryMapActor, Result> action)
    {
        // Arrange
        var map = AccessMap(Visibility.Public, StoryMapEditAccess.AnyoneWhoCanView);

        // Act
        var result = action(map, StoryMapActors.Employee(EditorEmployeeId));

        // Assert
        result.Error.Should().Be(StoryMap.NotAnOwnerError, name);
        map.Status.Should().Be(StatusCategory.Active);
        map.Visibility.Should().Be(Visibility.Public);
        map.Owners.Should().ContainSingle().Which.EmployeeId.Should().Be(StoryMapActors.OwnerEmployeeId);
        map.Editors.Should().ContainSingle();
    }

    [Theory]
    [MemberData(nameof(ManageActions))]
    public void ManageAction_ByOutsiderOnPrivateMap_ShouldFailAsNotFound(string name, Func<StoryMap, StoryMapActor, Result> action)
    {
        // Arrange
        var map = AccessMap(Visibility.Private, StoryMapEditAccess.AnyoneWhoCanView);

        // Act
        var result = action(map, StoryMapActors.Viewer);

        // Assert
        result.Error.Should().Be(StoryMap.NotFoundError, name);
    }

    [Theory]
    [MemberData(nameof(ManageActions))]
    public void ManageAction_ByAdministratorOnMapWithNoOwners_ShouldSucceed(string name, Func<StoryMap, StoryMapActor, Result> action)
    {
        // Arrange — as a map migrated from an owner with no linked employee is.
        var map = new StoryMapFaker().WithOwners().Generate();

        // Act
        var result = action(map, StoryMapActors.Administrator);

        // Assert
        result.IsSuccess.Should().BeTrue(name);
    }

    [Fact]
    public void SetOwners_ShouldReplaceTheOwners()
    {
        // Arrange
        var map = new StoryMapFaker().Generate();
        var other = Guid.NewGuid();

        // Act
        var result = map.SetOwners([other, other], StoryMapActors.Owner);

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.Owners.Should().ContainSingle().Which.EmployeeId.Should().Be(other);
    }

    [Fact]
    public void SetOwners_Empty_ShouldFailAndKeepTheOwners()
    {
        // Arrange
        var map = new StoryMapFaker().Generate();

        // Act
        var result = map.SetOwners([], StoryMapActors.Owner);

        // Assert
        result.IsFailure.Should().BeTrue();
        map.Owners.Should().ContainSingle().Which.EmployeeId.Should().Be(StoryMapActors.OwnerEmployeeId);
    }

    [Fact]
    public void SetEditors_ShouldReplaceEmployeesAndTeams()
    {
        // Arrange
        var map = AccessMap(Visibility.Public, StoryMapEditAccess.AnyoneWhoCanView);
        var employee = Guid.NewGuid();
        var team = Guid.NewGuid();

        // Act
        var result = map.SetEditors([employee], [team, team], StoryMapActors.Owner);

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.Editors.Select(e => e.EmployeeId).Should().Equal(employee);
        map.EditorTeams.Select(t => t.TeamId).Should().Equal(team);
    }

    [Fact]
    public void SetVisibility_OnArchivedMap_ShouldSucceed()
    {
        // Arrange
        var map = new StoryMapFaker().WithStatus(StatusCategory.Removed).Generate();

        // Act
        var result = map.SetVisibility(Visibility.Private, StoryMapActors.Owner);

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.Visibility.Should().Be(Visibility.Private);
    }

    #endregion Access
}
