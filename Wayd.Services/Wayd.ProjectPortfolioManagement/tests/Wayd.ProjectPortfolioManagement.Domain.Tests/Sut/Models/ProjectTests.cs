using FluentAssertions;
using NodaTime;
using NodaTime.Extensions;
using NodaTime.Testing;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Domain.Scoring;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Sut.Models;

public class ProjectTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly ProjectFaker _projectFaker;
    private readonly StrategicThemeFaker _themeFaker;
    private readonly ScoringModelFaker _scoringModelFaker;

    public ProjectTests()
    {
        _dateTimeProvider = new TestingDateTimeProvider(new FakeClock(DateTime.UtcNow.ToInstant()));
        _projectFaker = new ProjectFaker();
        _themeFaker = new StrategicThemeFaker();
        _scoringModelFaker = new ScoringModelFaker();
    }

    /// <summary>
    /// Creates a project with an assigned lifecycle containing the specified phases.
    /// Returns the project and the list of phases for easy access.
    /// </summary>
    private (Project Project, List<ProjectPhase> Phases) CreateProjectWithLifecycle(params (string Name, string Description)[] phases)
    {
        var project = _projectFaker.Generate();
        var lifecycle = new ProjectLifecycleFaker().AsActiveWithPhases(phases);
        project.AssignLifecycle(lifecycle);
        return (project, project.Phases.ToList());
    }

    #region Project Create and Update

    [Fact]
    public void Create_ShouldCreateProposedProjectSuccessfully()
    {
        // Arrange
        var name = "Test Project";
        var description = "Test Description";
        var key = new ProjectKey("TEST");
        var portfolioId = Guid.NewGuid();
        var expenditureCategoryId = 1;

        // Act
        var project = Project.Create(name, description, key, expenditureCategoryId, null, portfolioId, 1000d, null, null, null, null, null, _dateTimeProvider.Now);

        // Assert
        project.Should().NotBeNull();
        project.Name.Should().Be(name);
        project.Description.Should().Be(description);
        project.Key.Value.Should().Be(key);
        project.Status.Should().Be(ProjectStatus.Proposed);
        project.ExpenditureCategoryId.Should().Be(expenditureCategoryId);
        project.PortfolioId.Should().Be(portfolioId);
        project.ProgramId.Should().BeNull();
        project.DateRange.Should().BeNull();
    }

    [Fact]
    public void UpdateDetails_ShouldFail_WhenNameIsEmpty()
    {
        // Arrange
        var project = _projectFaker.Generate();

        // Act
        Action action = () => project.UpdateDetails("", "Valid Description", null, null, project.ExpenditureCategoryId, _dateTimeProvider.Now);

        // Assert
        action.Should().Throw<ArgumentException>().WithMessage("Required input Name was empty. (Parameter 'Name')");
    }

    [Fact]
    public void UpdateDetails_ShouldFail_WhenDescriptionIsEmpty()
    {
        // Arrange
        var project = _projectFaker.Generate();

        // Act
        Action action = () => project.UpdateDetails("Valid Name", "", null, null, project.ExpenditureCategoryId, _dateTimeProvider.Now);

        // Assert
        action.Should().Throw<ArgumentException>().WithMessage("Required input Description was empty. (Parameter 'Description')");
    }

    #endregion Project Create and Update

    #region UpdateTimeline Tests

    [Fact]
    public void UpdateTimeline_ShouldUpdatePlannedDatesSuccessfully_WhenProjectIsProposed()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var startDate = _dateTimeProvider.Today;
        var endDate = _dateTimeProvider.Today.PlusDays(30);
        var dateRange = new LocalDateRange(startDate, endDate);

        // Act
        var result = project.UpdateTimeline(dateRange);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.DateRange.Should().NotBeNull();
        project.DateRange!.Start.Should().Be(startDate);
        project.DateRange.End.Should().Be(endDate);
    }

    [Fact]
    public void UpdateTimeline_ShouldFail_WhenProjectIsActive_AndDatesAreNull()
    {
        // Arrange
        var project = _projectFaker.AsActive(_dateTimeProvider, Guid.NewGuid());

        // Act
        var result = project.UpdateTimeline(null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Active and completed projects must have a start and end date.");
    }

    [Fact]
    public void UpdateTimeline_ShouldFail_WhenProjectIsCompleted_AndDatesAreNull()
    {
        // Arrange
        var project = _projectFaker.AsCompleted(_dateTimeProvider, Guid.NewGuid());

        // Act
        var result = project.UpdateTimeline(null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Active and completed projects must have a start and end date.");
    }

    [Fact]
    public void UpdateTimeline_ShouldUpdateSuccessfully_WhenProjectIsActive_AndDatesAreValid()
    {
        // Arrange
        var project = _projectFaker.AsActive(_dateTimeProvider, Guid.NewGuid());
        var startDate = _dateTimeProvider.Today;
        var endDate = _dateTimeProvider.Today.PlusDays(60);
        var dateRange = new LocalDateRange(startDate, endDate);

        // Act
        var result = project.UpdateTimeline(dateRange);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.DateRange.Should().NotBeNull();
        project.DateRange!.Start.Should().Be(startDate);
        project.DateRange.End.Should().Be(endDate);
    }

    #endregion UpdateTimeline Tests

    #region Roles

    [Fact]
    public void AssignRole_ShouldAssignEmployeeToRoleSuccessfully()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var project = _projectFaker.Generate();

        // Act
        var result = project.AssignRole(ProjectRole.Owner, employeeId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Roles.Should().ContainSingle();
        project.Roles.First().Role.Should().Be(ProjectRole.Owner);
        project.Roles.First().EmployeeId.Should().Be(employeeId);
    }

    [Fact]
    public void AssignRole_ShouldFail_WhenEmployeeAlreadyAssignedToRole()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var project = _projectFaker.WithData(roles: new Dictionary<ProjectRole, HashSet<Guid>>
        {
            { ProjectRole.Owner, new HashSet<Guid> { employeeId } }
        }).Generate();

        // Act
        var result = project.AssignRole(ProjectRole.Owner, employeeId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Employee is already assigned to this role.");
    }

    [Fact]
    public void RemoveRole_WithOneRoleAssignment_ShouldRemoveEmployeeFromRoleSuccessfully()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var project = _projectFaker.WithData(roles: new Dictionary<ProjectRole, HashSet<Guid>>
        {
            { ProjectRole.Owner, new HashSet<Guid> { employeeId } }
        }).Generate();

        // Act
        var result = project.RemoveRole(ProjectRole.Owner, employeeId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Roles.Should().BeEmpty();
    }

    [Fact]
    public void RemoveRole_WithMultipleRoleAssignments_ShouldRemoveEmployeeFromRoleSuccessfully()
    {
        // Arrange
        var employeeId1 = Guid.NewGuid();
        var employeeId2 = Guid.NewGuid();
        var project = _projectFaker.WithData(roles: new Dictionary<ProjectRole, HashSet<Guid>>
        {
            { ProjectRole.Owner, new HashSet<Guid> { employeeId1, employeeId2 } }
        }).Generate();

        // Act
        var result = project.RemoveRole(ProjectRole.Owner, employeeId1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Roles.Count.Should().Be(1);
        project.Roles.First().Role.Should().Be(ProjectRole.Owner);
        project.Roles.First().EmployeeId.Should().Be(employeeId2);
    }

    [Fact]
    public void RemoveRole_ShouldFail_WhenEmployeeNotAssignedToRole()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var project = _projectFaker.Generate();

        // Act
        var result = project.RemoveRole(ProjectRole.Owner, employeeId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Employee is not assigned to this role.");
    }


    [Fact]
    public void UpdateRoles_ShouldAssignNewRolesSuccessfully()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var employee1 = Guid.NewGuid();
        var employee2 = Guid.NewGuid();
        var updatedRoles = new Dictionary<ProjectRole, HashSet<Guid>>
        {
            { ProjectRole.Manager, new HashSet<Guid> { employee1, employee2 } }
        };

        // Act
        var result = project.UpdateRoles(updatedRoles);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Roles.Should().Contain(role => role.Role == ProjectRole.Manager && role.EmployeeId == employee1);
        project.Roles.Should().Contain(role => role.Role == ProjectRole.Manager && role.EmployeeId == employee2);
    }

    [Fact]
    public void UpdateRoles_ShouldRemoveUnspecifiedRoles()
    {
        // Arrange
        var project = _projectFaker.WithData(roles: new Dictionary<ProjectRole, HashSet<Guid>>
        {
            { ProjectRole.Manager, new HashSet<Guid> { Guid.NewGuid(), Guid.NewGuid() } },
            { ProjectRole.Owner, new HashSet<Guid> { Guid.NewGuid() } }
        }).Generate();

        var updatedRoles = new Dictionary<ProjectRole, HashSet<Guid>>
        {
            { ProjectRole.Manager, new HashSet<Guid> { Guid.NewGuid() } }  // Remove Owner role
        };

        // Act
        var result = project.UpdateRoles(updatedRoles);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Roles.Should().Contain(role => role.Role == ProjectRole.Manager);
        project.Roles.Should().NotContain(role => role.Role == ProjectRole.Owner); // Removed role
    }

    [Fact]
    public void UpdateRoles_ShouldNotChange_WhenRolesAreUnchanged()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var project = _projectFaker.WithData(roles: new Dictionary<ProjectRole, HashSet<Guid>>
        {
            { ProjectRole.Sponsor, new HashSet<Guid> { employeeId } }
        }).Generate();

        var updatedRoles = new Dictionary<ProjectRole, HashSet<Guid>>
        {
            { ProjectRole.Sponsor, new HashSet<Guid> { employeeId } }
        };

        // Act
        var result = project.UpdateRoles(updatedRoles);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Roles.Count.Should().Be(1);
        project.Roles.Should().Contain(role => role.Role == ProjectRole.Sponsor && role.EmployeeId == employeeId);
    }

    [Fact]
    public void UpdateRoles_ShouldFail_WhenInvalidRoleProvided()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var invalidRole = (ProjectRole)999;
        var updatedRoles = new Dictionary<ProjectRole, HashSet<Guid>>
        {
            { invalidRole, new HashSet<Guid> { Guid.NewGuid() } }
        };

        // Act
        var result = project.UpdateRoles(updatedRoles);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be($"Role is not a valid {nameof(ProjectRole)} value.");
    }

    #endregion Roles

    #region Lifecycle Tests

    [Fact]
    public void Activate_ShouldActivateProposedProjectSuccessfully()
    {
        // Arrange
        var dateRange = new LocalDateRange(_dateTimeProvider.Today, _dateTimeProvider.Today.PlusMonths(3));
        var project = _projectFaker.WithData(dateRange: dateRange).Generate();

        // Act
        var result = project.Activate();

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Status.Should().Be(ProjectStatus.Active);
        project.DateRange.Should().NotBeNull();
        project.DateRange.Should().Be(dateRange);
    }

    [Fact]
    public void Activate_ShouldActivateApprovedProjectSuccessfully()
    {
        // Arrange
        var project = _projectFaker.AsApproved(_dateTimeProvider, Guid.NewGuid());

        // Act
        var result = project.Activate();

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Status.Should().Be(ProjectStatus.Active);
    }

    [Fact]
    public void Activate_ShouldFail_WhenProjectIsNotProposedOrApproved()
    {
        // Arrange
        var project = _projectFaker.AsActive(_dateTimeProvider, Guid.NewGuid());

        // Act
        var result = project.Activate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only proposed or approved projects can be activated.");
    }

    [Fact]
    public void Approve_ShouldApproveProposedProjectSuccessfully()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var lifecycle = new ProjectLifecycleFaker().AsActiveWithPhases(("Plan", "Plan phase"), ("Execute", "Execute phase"));
        project.AssignLifecycle(lifecycle);

        // Act
        var result = project.Approve();

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Status.Should().Be(ProjectStatus.Approved);
    }

    [Fact]
    public void Approve_ShouldFail_WhenNoLifecycleAssigned()
    {
        // Arrange
        var project = _projectFaker.Generate();

        // Act
        var result = project.Approve();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("lifecycle");
    }

    [Fact]
    public void Approve_ShouldFail_WhenProjectIsNotProposed()
    {
        // Arrange
        var project = _projectFaker.AsActive(_dateTimeProvider, Guid.NewGuid());

        // Act
        var result = project.Approve();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only proposed projects can be approved.");
    }

    [Fact]
    public void Cancel_ShouldCancelApprovedProjectSuccessfully()
    {
        // Arrange
        var project = _projectFaker.AsApproved(_dateTimeProvider, Guid.NewGuid());

        // Act
        var result = project.Cancel();

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Status.Should().Be(ProjectStatus.Cancelled);
    }

    [Fact]
    public void Complete_ShouldCompleteActiveProjectSuccessfully()
    {
        // Arrange
        var project = _projectFaker.AsActive(_dateTimeProvider, Guid.NewGuid());

        // Act
        var result = project.Complete();

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Status.Should().Be(ProjectStatus.Completed);
    }

    [Fact]
    public void Complete_ShouldFail_WhenProjectIsNotActive()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var endDate = _dateTimeProvider.Today.PlusDays(10);

        // Act
        var result = project.Complete();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only active projects can be completed.");
    }

    [Fact]
    public void Cancel_ShouldCancelActiveProjectSuccessfully()
    {
        // Arrange
        var project = _projectFaker.AsActive(_dateTimeProvider, Guid.NewGuid());

        // Act
        var result = project.Cancel();

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Status.Should().Be(ProjectStatus.Cancelled);
    }

    [Fact]
    public void Cancel_ShouldFail_WhenProjectIsAlreadyCompletedOrCancelled()
    {
        // Arrange
        var project = _projectFaker.AsCancelled(_dateTimeProvider, Guid.NewGuid());
        var endDate = _dateTimeProvider.Today.PlusDays(10);

        // Act
        var result = project.Cancel();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The project is already completed or cancelled.");
    }

    #endregion Lifecycle Tests

    #region Program Association Tests

    [Fact]
    public void UpdateProgram_ShouldAssociateProjectWithProgramSuccessfully()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var program = Program.Create("Test Program", "Description", null, project.PortfolioId, null, null, _dateTimeProvider.Now);

        // Act
        var result = project.UpdateProgram(program);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.ProgramId.Should().Be(program.Id);
    }

    [Fact]
    public void UpdateProgram_ShouldFail_WhenProgramIsInDifferentPortfolio()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var portfolioId = Guid.NewGuid();
        var program = Program.Create("Test Program", "Description", null, portfolioId, null, null, _dateTimeProvider.Now);

        // Act
        var result = project.UpdateProgram(program);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The project must belong to the same portfolio as the program.");
    }

    [Fact]
    public void UpdateProgram_ShouldRemoveProgramAssociation_WhenNullProgramPassed()
    {
        // Arrange
        var project = _projectFaker.WithData(programId: Guid.NewGuid()).Generate();

        // Act
        var result = project.UpdateProgram(null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.ProgramId.Should().BeNull();
    }

    #endregion Program Association Tests

    #region Strategic Theme Management

    [Fact]
    public void UpdateStrategicThemes_ShouldUpdateThemesSuccessfully()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var themes = _themeFaker.Generate(3); // Generate 3 unique themes

        // Act
        var result = project.UpdateStrategicThemes(themes.Select(t => t.Id).ToHashSet());

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.StrategicThemeTags.Should().HaveCount(3);
        project.StrategicThemeTags.Select(t => t.StrategicThemeId).Should().BeEquivalentTo(themes.Select(t => t.Id));
    }

    [Fact]
    public void UpdateStrategicThemes_ShouldRemoveExistingThemes_WhenNewThemesAreAdded()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var initialThemes = _themeFaker.Generate(2);
        project.UpdateStrategicThemes(initialThemes.Select(t => t.Id).ToHashSet());

        var newThemes = _themeFaker.Generate(3); // Replace with different themes

        // Act
        var result = project.UpdateStrategicThemes(newThemes.Select(t => t.Id).ToHashSet());

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.StrategicThemeTags.Should().HaveCount(3);
        project.StrategicThemeTags.Select(t => t.StrategicThemeId).Should().BeEquivalentTo(newThemes.Select(t => t.Id));
    }

    [Fact]
    public void UpdateStrategicThemes_ShouldSucceed_WhenNoChangesAreMade()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var themes = _themeFaker.Generate(2);
        project.UpdateStrategicThemes(themes.Select(t => t.Id).ToHashSet());

        // Act
        var result = project.UpdateStrategicThemes(themes.Select(t => t.Id).ToHashSet()); // Same themes

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.StrategicThemeTags.Should().HaveCount(2);
    }

    [Fact]
    public void UpdateStrategicThemes_ShouldHandleEmptyListCorrectly()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var initialThemes = _themeFaker.Generate(2);
        project.UpdateStrategicThemes(initialThemes.Select(t => t.Id).ToHashSet());

        // Act
        var result = project.UpdateStrategicThemes([]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.StrategicThemeTags.Should().BeEmpty();
    }

    #endregion Strategic Theme Management

    #region Key Management

    [Fact]
    public void ChangeKey_ShouldUpdateProjectKeySuccessfully()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var originalKey = project.Key;
        var newKey = new ProjectKey("NEWPROJ");

        // Act
        var result = project.ChangeKey(newKey, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Key.Should().Be(newKey);
        project.Key.Should().NotBe(originalKey);
    }

    [Fact]
    public void ChangeKey_ShouldBeNoOp_WhenKeyIsUnchanged()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var originalKey = project.Key;

        // Act
        var result = project.ChangeKey(originalKey, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Key.Should().Be(originalKey);
    }

    [Fact]
    public void ChangeKey_ShouldUpdateAllTaskKeys_WhenProjectHasTasks()
    {
        // Arrange
        var (project, phases) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var phaseId = phases[0].Id;

        var task1 = project.CreateTask(
            nextNumber: 1,
            name: "Task 1",
            description: null,
            type: ProjectTaskType.Task,
            status: Enums.TaskStatus.NotStarted,
            priority: TaskPriority.Medium,
            progress: null,
            parentId: phaseId,
            plannedDateRange: null,
            plannedDate: null,
            estimatedEffortHours: null,
            roles: null).Value;

        var task2 = project.CreateTask(
            nextNumber: 2,
            name: "Task 2",
            description: null,
            type: ProjectTaskType.Task,
            status: Enums.TaskStatus.NotStarted,
            priority: TaskPriority.Medium,
            progress: null,
            parentId: phaseId,
            plannedDateRange: null,
            plannedDate: null,
            estimatedEffortHours: null,
            roles: null).Value;

        var newKey = new ProjectKey("NEWTASKS");

        // Act
        var result = project.ChangeKey(newKey, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Key.Should().Be(newKey);

        task1.Key.Value.Should().Be($"{newKey.Value}-1");
        task2.Key.Value.Should().Be($"{newKey.Value}-2");
    }

    #endregion Key Management

    #region ChangeTaskPlacement Tests

    [Fact]
    public void ChangeTaskPlacement_ShouldFail_WhenTaskNotFound()
    {
        // Arrange
        var (project, phases) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var nonExistentTaskId = Guid.NewGuid();

        // Act
        var result = project.ChangeTaskPlacement(nonExistentTaskId, phases[0].Id, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Task not found.");
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldFail_WhenOrderIsZero()
    {
        // Arrange
        var (project, phases) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var phaseId = phases[0].Id;
        var task = project.CreateTask(1, "Task 1", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, phaseId, null, null, null, null).Value;

        // Act
        var result = project.ChangeTaskPlacement(task.Id, phaseId, 0);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Order must be greater than zero.");
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldFail_WhenOrderIsNegative()
    {
        // Arrange
        var (project, phases) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var phaseId = phases[0].Id;
        var task = project.CreateTask(1, "Task 1", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, phaseId, null, null, null, null).Value;

        // Act
        var result = project.ChangeTaskPlacement(task.Id, phaseId, -1);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Order must be greater than zero.");
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldFail_WhenNewParentNotFound()
    {
        // Arrange
        var (project, phases) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var phaseId = phases[0].Id;
        var task = project.CreateTask(1, "Task 1", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, phaseId, null, null, null, null).Value;
        var nonExistentParentId = Guid.NewGuid();

        // Act
        var result = project.ChangeTaskPlacement(task.Id, nonExistentParentId, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldFail_WhenNewParentIsMilestone()
    {
        // Arrange
        var (project, phases) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var phaseId = phases[0].Id;

        var milestoneTask = project.CreateTask(1, "Milestone", null, ProjectTaskType.Milestone, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, phaseId, null, _dateTimeProvider.Today.PlusDays(30), null, null).Value;
        var regularTask = project.CreateTask(2, "Regular Task", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, phaseId, null, null, null, null).Value;

        // Act
        var result = project.ChangeTaskPlacement(regularTask.Id, milestoneTask.Id, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Milestones cannot have child tasks.");
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldSucceed_WhenMovingTaskToNewParent()
    {
        // Arrange
        var (project, phases) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var phaseId = phases[0].Id;
        var tasks = project.WithTasks(3, phaseId);
        var parentTask = tasks[0];
        var taskToMove = tasks[1];

        // Act
        var result = project.ChangeTaskPlacement(taskToMove.Id, parentTask.Id, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        taskToMove.ParentId.Should().Be(parentTask.Id);
        taskToMove.Order.Should().Be(1); // First child of new parent
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldSucceed_WhenMovingTaskToRoot()
    {
        // Arrange
        var (project, phases) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var phaseId = phases[0].Id;

        var parentTask = new ProjectTaskFaker()
            .WithData(id: Guid.NewGuid(), projectId: project.Id, key: new ProjectTaskKey(project.Key, 1), order: 1, projectPhaseId: phaseId)
            .Generate();
        project.AddToPrivateList("_tasks", parentTask);

        var childTask = new ProjectTaskFaker()
            .WithData(id: Guid.NewGuid(), projectId: project.Id, key: new ProjectTaskKey(project.Key, 2), order: 1, parentId: parentTask.Id, projectPhaseId: phaseId)
            .Generate();
        project.AddToPrivateList("_tasks", childTask);
        parentTask.AddChild(childTask);

        // Act - Move child to root of phase
        var result = project.ChangeTaskPlacement(childTask.Id, phaseId, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        childTask.ParentId.Should().BeNull();
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldSucceed_WhenChangingOrderWithinSameParent_MovingUp()
    {
        // Arrange
        var (project, phases) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var phaseId = phases[0].Id;
        var tasks = project.WithTasks(3, phaseId);
        var task1 = tasks[0]; // Order 1
        var task2 = tasks[1]; // Order 2
        var task3 = tasks[2]; // Order 3

        // Act - Move task3 to position 1
        var result = project.ChangeTaskPlacement(task3.Id, phaseId, 1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task3.Order.Should().Be(1);
        task1.Order.Should().Be(2); // Shifted down
        task2.Order.Should().Be(3); // Shifted down
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldSucceed_WhenChangingOrderWithinSameParent_MovingDown()
    {
        // Arrange
        var (project, phases) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var phaseId = phases[0].Id;
        var tasks = project.WithTasks(3, phaseId);
        var task1 = tasks[0]; // Order 1
        var task2 = tasks[1]; // Order 2
        var task3 = tasks[2]; // Order 3

        // Act - Move task1 to position 3
        var result = project.ChangeTaskPlacement(task1.Id, phaseId, 3);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task1.Order.Should().Be(3);
        task2.Order.Should().Be(1); // Shifted up
        task3.Order.Should().Be(2); // Shifted up
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldSucceed_WhenOrderIsNull_DefaultsToEnd()
    {
        // Arrange
        var (project, phases) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var phaseId = phases[0].Id;

        var parentTask = new ProjectTaskFaker()
            .WithData(id: Guid.NewGuid(), projectId: project.Id, key: new ProjectTaskKey(project.Key, 1), order: 1, projectPhaseId: phaseId)
            .Generate();
        project.AddToPrivateList("_tasks", parentTask);

        var existingChild = new ProjectTaskFaker()
            .WithData(id: Guid.NewGuid(), projectId: project.Id, key: new ProjectTaskKey(project.Key, 2), order: 1, parentId: parentTask.Id, projectPhaseId: phaseId)
            .Generate();
        project.AddToPrivateList("_tasks", existingChild);
        parentTask.AddChild(existingChild);

        var taskToMove = new ProjectTaskFaker()
            .WithData(id: Guid.NewGuid(), projectId: project.Id, key: new ProjectTaskKey(project.Key, 3), order: 2, projectPhaseId: phaseId)
            .Generate();
        project.AddToPrivateList("_tasks", taskToMove);

        // Act - Move task to parent without specifying order
        var result = project.ChangeTaskPlacement(taskToMove.Id, parentTask.Id, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        taskToMove.ParentId.Should().Be(parentTask.Id);
        taskToMove.Order.Should().Be(2); // Added at the end
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldClampOrder_WhenOrderExceedsChildrenCount()
    {
        // Arrange
        var (project, phases) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var phaseId = phases[0].Id;
        var tasks = project.WithTasks(2, phaseId);
        var task1 = tasks[0];

        // Act - Try to move task1 to position 10 (only 2 tasks exist)
        var result = project.ChangeTaskPlacement(task1.Id, phaseId, 10);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task1.Order.Should().Be(2); // Clamped to max valid position
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldReturnSuccess_WhenNoChangeNeeded()
    {
        // Arrange
        var (project, phases) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var phaseId = phases[0].Id;
        var tasks = project.WithTasks(3, phaseId);
        var task2 = tasks[1]; // Order 2

        // Act - Request same order
        var result = project.ChangeTaskPlacement(task2.Id, phaseId, 2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task2.Order.Should().Be(2);
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldUpdateOldParentChildren_WhenMovingToNewParent()
    {
        // Arrange
        var (project, phases) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var phaseId = phases[0].Id;

        var oldParent = new ProjectTaskFaker()
            .WithData(id: Guid.NewGuid(), projectId: project.Id, key: new ProjectTaskKey(project.Key, 1), order: 1, projectPhaseId: phaseId)
            .Generate();
        project.AddToPrivateList("_tasks", oldParent);

        var child1 = new ProjectTaskFaker()
            .WithData(id: Guid.NewGuid(), projectId: project.Id, key: new ProjectTaskKey(project.Key, 2), order: 1, parentId: oldParent.Id, projectPhaseId: phaseId)
            .Generate();
        project.AddToPrivateList("_tasks", child1);
        oldParent.AddChild(child1);

        var child2 = new ProjectTaskFaker()
            .WithData(id: Guid.NewGuid(), projectId: project.Id, key: new ProjectTaskKey(project.Key, 3), order: 2, parentId: oldParent.Id, projectPhaseId: phaseId)
            .Generate();
        project.AddToPrivateList("_tasks", child2);
        oldParent.AddChild(child2);

        var child3 = new ProjectTaskFaker()
            .WithData(id: Guid.NewGuid(), projectId: project.Id, key: new ProjectTaskKey(project.Key, 4), order: 3, parentId: oldParent.Id, projectPhaseId: phaseId)
            .Generate();
        project.AddToPrivateList("_tasks", child3);
        oldParent.AddChild(child3);

        var newParent = new ProjectTaskFaker()
            .WithData(id: Guid.NewGuid(), projectId: project.Id, key: new ProjectTaskKey(project.Key, 5), order: 2, projectPhaseId: phaseId)
            .Generate();
        project.AddToPrivateList("_tasks", newParent);

        // Act - Move child2 from oldParent to newParent
        var result = project.ChangeTaskPlacement(child2.Id, newParent.Id, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        child2.ParentId.Should().Be(newParent.Id);
        child1.Order.Should().Be(1); // Unchanged
        child3.Order.Should().Be(2); // Order reset to be consecutive after child2 was moved
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldMoveTaskToSpecificPosition_WhenMovingToNewParent()
    {
        // Arrange
        var (project, phases) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var phaseId = phases[0].Id;

        var newParent = new ProjectTaskFaker()
            .WithData(id: Guid.NewGuid(), projectId: project.Id, key: new ProjectTaskKey(project.Key, 1), order: 1, projectPhaseId: phaseId)
            .Generate();
        project.AddToPrivateList("_tasks", newParent);

        var existingChild1 = new ProjectTaskFaker()
            .WithData(id: Guid.NewGuid(), projectId: project.Id, key: new ProjectTaskKey(project.Key, 2), order: 1, parentId: newParent.Id, projectPhaseId: phaseId)
            .Generate();
        project.AddToPrivateList("_tasks", existingChild1);
        newParent.AddChild(existingChild1);

        var existingChild2 = new ProjectTaskFaker()
            .WithData(id: Guid.NewGuid(), projectId: project.Id, key: new ProjectTaskKey(project.Key, 3), order: 2, parentId: newParent.Id, projectPhaseId: phaseId)
            .Generate();
        project.AddToPrivateList("_tasks", existingChild2);
        newParent.AddChild(existingChild2);

        var taskToMove = new ProjectTaskFaker()
            .WithData(id: Guid.NewGuid(), projectId: project.Id, key: new ProjectTaskKey(project.Key, 4), order: 2, projectPhaseId: phaseId)
            .Generate();
        project.AddToPrivateList("_tasks", taskToMove);

        // Act - Move task to position 1 under newParent
        var result = project.ChangeTaskPlacement(taskToMove.Id, newParent.Id, 1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        taskToMove.ParentId.Should().Be(newParent.Id);
        taskToMove.Order.Should().Be(1);
        existingChild1.Order.Should().Be(2); // Shifted
        existingChild2.Order.Should().Be(3); // Shifted
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldFail_WhenTaskIsItsOwnParent()
    {
        // Arrange
        var (project, phases) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var tasks = project.WithTasks(1, phases[0].Id);
        var task = tasks[0];

        // Act
        var result = project.ChangeTaskPlacement(task.Id, task.Id, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A task cannot be its own parent.");
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldFail_WhenMovingToDescendant()
    {
        // Arrange
        var (project, phases) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var phaseId = phases[0].Id;

        var parentTask = new ProjectTaskFaker()
            .WithData(id: Guid.NewGuid(), projectId: project.Id, key: new ProjectTaskKey(project.Key, 1), order: 1, projectPhaseId: phaseId)
            .Generate();
        project.AddToPrivateList("_tasks", parentTask);

        var childTask = new ProjectTaskFaker()
            .WithData(id: Guid.NewGuid(), projectId: project.Id, key: new ProjectTaskKey(project.Key, 2), order: 1, parentId: parentTask.Id, projectPhaseId: phaseId)
            .Generate();
        project.AddToPrivateList("_tasks", childTask);
        parentTask.AddChild(childTask);

        var grandchildTask = new ProjectTaskFaker()
            .WithData(id: Guid.NewGuid(), projectId: project.Id, key: new ProjectTaskKey(project.Key, 3), order: 1, parentId: childTask.Id, projectPhaseId: phaseId)
            .Generate();
        project.AddToPrivateList("_tasks", grandchildTask);
        childTask.AddChild(grandchildTask);

        // Act - Try to move parent under its grandchild (circular reference)
        var result = project.ChangeTaskPlacement(parentTask.Id, grandchildTask.Id, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A task cannot be moved under one of its descendants.");
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldHandleSingleTask_WhenChangingOrder()
    {
        // Arrange
        var (project, phases) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var phaseId = phases[0].Id;
        var tasks = project.WithTasks(1, phaseId);
        var task = tasks[0];

        // Act - Try to change order of only task
        var result = project.ChangeTaskPlacement(task.Id, phaseId, 1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task.Order.Should().Be(1);
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldMoveToMiddlePosition_WhenMovingUp()
    {
        // Arrange
        var (project, phases) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var phaseId = phases[0].Id;
        var tasks = project.WithTasks(5, phaseId);
        var task1 = tasks[0]; // Order 1
        var task2 = tasks[1]; // Order 2
        var task3 = tasks[2]; // Order 3
        var task4 = tasks[3]; // Order 4
        var task5 = tasks[4]; // Order 5

        // Act - Move task5 to position 2
        var result = project.ChangeTaskPlacement(task5.Id, phaseId, 2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task1.Order.Should().Be(1); // Unchanged
        task5.Order.Should().Be(2); // Moved here
        task2.Order.Should().Be(3); // Shifted down
        task3.Order.Should().Be(4); // Shifted down
        task4.Order.Should().Be(5); // Shifted down
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldMoveToMiddlePosition_WhenMovingDown()
    {
        // Arrange
        var (project, phases) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var phaseId = phases[0].Id;
        var tasks = project.WithTasks(5, phaseId);
        var task1 = tasks[0]; // Order 1
        var task2 = tasks[1]; // Order 2
        var task3 = tasks[2]; // Order 3
        var task4 = tasks[3]; // Order 4
        var task5 = tasks[4]; // Order 5

        // Act - Move task1 to position 4
        var result = project.ChangeTaskPlacement(task1.Id, phaseId, 4);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task2.Order.Should().Be(1); // Shifted up
        task3.Order.Should().Be(2); // Shifted up
        task4.Order.Should().Be(3); // Shifted up
        task1.Order.Should().Be(4); // Moved here
        task5.Order.Should().Be(5); // Unchanged
    }

    #endregion ChangeTaskPlacement Tests

    #region AssignLifecycle Tests

    [Fact]
    public void AssignLifecycle_ShouldSucceed_WhenProposedWithActiveLifecycle()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var lifecycle = new ProjectLifecycleFaker().AsActiveWithPhases(
            ("Plan", "Define goals"),
            ("Execute", "Perform the work"),
            ("Deliver", "Release outcome"));

        // Act
        var result = project.AssignLifecycle(lifecycle);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.ProjectLifecycleId.Should().Be(lifecycle.Id);
        project.Phases.Should().HaveCount(3);
        project.Phases.Select(p => p.Name).Should().ContainInOrder("Plan", "Execute", "Deliver");
        project.Phases.Select(p => p.Order).Should().ContainInOrder(1, 2, 3);
        project.Phases.Should().AllSatisfy(p =>
        {
            p.ProjectId.Should().Be(project.Id);
            p.Status.Should().Be(Enums.TaskStatus.NotStarted);
        });
    }

    [Fact]
    public void AssignLifecycle_ShouldFail_WhenLifecycleIsNotActive()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var lifecycle = ProjectLifecycle.Create("Test", "Description", [("Phase 1", "Description")]);

        // Act
        var result = project.AssignLifecycle(lifecycle);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("active");
    }

    [Fact]
    public void AssignLifecycle_ShouldFail_WhenLifecycleAlreadyAssigned()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var lifecycle = new ProjectLifecycleFaker().AsActiveWithPhases(("Phase 1", "Description"));
        project.AssignLifecycle(lifecycle);

        var anotherLifecycle = new ProjectLifecycleFaker().AsActiveWithPhases(("Phase A", "Description"));

        // Act
        var result = project.AssignLifecycle(anotherLifecycle);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already assigned");
    }

    [Fact]
    public void AssignLifecycle_ShouldFail_WhenProjectIsClosed()
    {
        // Arrange
        var project = _projectFaker.AsCompleted(_dateTimeProvider);

        var lifecycle = new ProjectLifecycleFaker().AsActiveWithPhases(("Phase 1", "Description"));

        // Act
        var result = project.AssignLifecycle(lifecycle);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("closed");
    }

    #endregion AssignLifecycle Tests

    #region CreateTask with Phase Tests

    [Fact]
    public void CreateTask_ShouldSucceed_WhenRootTaskWithPhase()
    {
        // Arrange
        var (project, phases) = CreateProjectWithLifecycle(
            ("Plan", "Planning phase"),
            ("Execute", "Execution phase"));
        var executePhase = phases.First(p => p.Name == "Execute");

        // Act
        var result = project.CreateTask(
            nextNumber: 1,
            name: "Task 1",
            description: null,
            type: ProjectTaskType.Task,
            status: Enums.TaskStatus.NotStarted,
            priority: TaskPriority.Medium,
            progress: null,
            parentId: executePhase.Id,
            plannedDateRange: null,
            plannedDate: null,
            estimatedEffortHours: null,
            roles: null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ProjectPhaseId.Should().Be(executePhase.Id);
    }

    [Fact]
    public void CreateTask_ShouldFail_WhenNoLifecycleAssigned()
    {
        // Arrange
        var project = _projectFaker.Generate();

        // Act
        var result = project.CreateTask(
            nextNumber: 1,
            name: "Task 1",
            description: null,
            type: ProjectTaskType.Task,
            status: Enums.TaskStatus.NotStarted,
            priority: TaskPriority.Medium,
            progress: null,
            parentId: Guid.NewGuid(),
            plannedDateRange: null,
            plannedDate: null,
            estimatedEffortHours: null,
            roles: null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("lifecycle");
    }

    [Fact]
    public void CreateTask_ShouldFail_WhenParentIdDoesNotMatchPhaseOrTask()
    {
        // Arrange
        var (project, _) = CreateProjectWithLifecycle(("Phase 1", "Description"));

        // Act
        var result = project.CreateTask(
            nextNumber: 1,
            name: "Task 1",
            description: null,
            type: ProjectTaskType.Task,
            status: Enums.TaskStatus.NotStarted,
            priority: TaskPriority.Medium,
            progress: null,
            parentId: Guid.NewGuid(), // Random ID that doesn't match any phase or task
            plannedDateRange: null,
            plannedDate: null,
            estimatedEffortHours: null,
            roles: null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public void CreateTask_ShouldInheritPhase_WhenChildTask()
    {
        // Arrange
        var (project, phases) = CreateProjectWithLifecycle(("Execute", "Execution phase"));
        var executePhase = phases.First();

        var parentResult = project.CreateTask(
            nextNumber: 1,
            name: "Parent Task",
            description: null,
            type: ProjectTaskType.Task,
            status: Enums.TaskStatus.NotStarted,
            priority: TaskPriority.Medium,
            progress: null,
            parentId: executePhase.Id,
            plannedDateRange: null,
            plannedDate: null,
            estimatedEffortHours: null,
            roles: null);

        // Act
        var childResult = project.CreateTask(
            nextNumber: 2,
            name: "Child Task",
            description: null,
            type: ProjectTaskType.Task,
            status: Enums.TaskStatus.NotStarted,
            priority: TaskPriority.Medium,
            progress: null,
            parentId: parentResult.Value.Id, // Parent task ID — should inherit phase
            plannedDateRange: null,
            plannedDate: null,
            estimatedEffortHours: null,
            roles: null);

        // Assert
        childResult.IsSuccess.Should().BeTrue();
        childResult.Value.ProjectPhaseId.Should().Be(executePhase.Id);
    }

    [Fact]
    public void CreateTask_ShouldScopeOrderToPhase_WhenRootTasks()
    {
        // Arrange
        var (project, phases) = CreateProjectWithLifecycle(
            ("Plan", "Planning"),
            ("Execute", "Execution"));
        var planPhase = phases.First(p => p.Name == "Plan");
        var executePhase = phases.First(p => p.Name == "Execute");

        // Create tasks in Plan phase
        project.CreateTask(1, "Plan Task 1", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, planPhase.Id, null, null, null, null);
        project.CreateTask(2, "Plan Task 2", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, planPhase.Id, null, null, null, null);

        // Act — Create first task in Execute phase
        var result = project.CreateTask(3, "Execute Task 1", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, executePhase.Id, null, null, null, null);

        // Assert — Order should be 1 (scoped to Execute phase), not 3
        result.IsSuccess.Should().BeTrue();
        result.Value.Order.Should().Be(1);
    }

    #endregion CreateTask with Phase Tests

    #region ChangeTaskPlacement Phase Tests

    [Fact]
    public void ChangeTaskPlacement_ShouldSucceed_WhenMovingRootTaskToAnotherPhase()
    {
        // Arrange
        var (project, phases) = CreateProjectWithLifecycle(("Plan", "Planning"), ("Execute", "Execution"));
        var planPhase = phases.First(p => p.Name == "Plan");
        var executePhase = phases.First(p => p.Name == "Execute");
        var tasks = project.WithTasks(1, planPhase.Id);
        var task = tasks[0];

        // Act - Move root task to Execute phase
        var result = project.ChangeTaskPlacement(task.Id, executePhase.Id, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task.ProjectPhaseId.Should().Be(executePhase.Id);
        task.ParentId.Should().BeNull();
        task.Order.Should().Be(1);
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldMoveDescendants_WhenMovingRootTaskToAnotherPhase()
    {
        // Arrange
        var (project, phases) = CreateProjectWithLifecycle(("Plan", "Planning"), ("Execute", "Execution"));
        var planPhase = phases.First(p => p.Name == "Plan");
        var executePhase = phases.First(p => p.Name == "Execute");

        var parent = new ProjectTaskFaker()
            .WithData(id: Guid.NewGuid(), projectId: project.Id, key: new ProjectTaskKey(project.Key, 1), order: 1, projectPhaseId: planPhase.Id)
            .Generate();
        project.AddToPrivateList("_tasks", parent);

        var child = new ProjectTaskFaker()
            .WithData(id: Guid.NewGuid(), projectId: project.Id, key: new ProjectTaskKey(project.Key, 2), order: 1, parentId: parent.Id, projectPhaseId: planPhase.Id)
            .Generate();
        project.AddToPrivateList("_tasks", child);
        parent.AddChild(child);

        // Act - Move parent (and its descendants) to Execute phase
        var result = project.ChangeTaskPlacement(parent.Id, executePhase.Id, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        parent.ProjectPhaseId.Should().Be(executePhase.Id);
        child.ProjectPhaseId.Should().Be(executePhase.Id);
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldReorderOldPhase_WhenTaskMoved()
    {
        // Arrange
        var (project, phases) = CreateProjectWithLifecycle(("Plan", "Planning"), ("Execute", "Execution"));
        var planPhase = phases.First(p => p.Name == "Plan");
        var executePhase = phases.First(p => p.Name == "Execute");
        var tasks = project.WithTasks(3, planPhase.Id);
        var task1 = tasks[0];
        var task2 = tasks[1];
        var task3 = tasks[2];

        // Act — Move task2 to Execute phase
        var result = project.ChangeTaskPlacement(task2.Id, executePhase.Id, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task1.Order.Should().Be(1);
        task3.Order.Should().Be(2); // Reordered to fill gap
        task2.Order.Should().Be(1); // First in new phase
        task2.ProjectPhaseId.Should().Be(executePhase.Id);
    }

    [Fact]
    public void ChangeTaskPlacement_ShouldBeNoOp_WhenSamePhaseAndNoOrderChange()
    {
        // Arrange
        var (project, phases) = CreateProjectWithLifecycle(("Plan", "Planning"));
        var phaseId = phases[0].Id;
        var tasks = project.WithTasks(1, phaseId);

        // Act - Move to same phase with same order
        var result = project.ChangeTaskPlacement(tasks[0].Id, phaseId, 1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        tasks[0].ProjectPhaseId.Should().Be(phaseId);
    }

    #endregion ChangeTaskPlacement Phase Tests

    #region ChangeLifecycle Tests

    [Fact]
    public void ChangeLifecycle_ShouldSucceed_WhenMappingIsValid()
    {
        // Arrange
        var (project, oldPhases) = CreateProjectWithLifecycle(
            ("Plan", "Planning phase"),
            ("Execute", "Execution phase"),
            ("Deliver", "Delivery phase"));

        var oldPhase1 = oldPhases[0];
        var oldPhase2 = oldPhases[1];

        // Create tasks in the first two phases
        project.CreateTask(1, "Task 1", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, oldPhase1.Id, null, null, null, null);
        project.CreateTask(2, "Task 2", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, oldPhase2.Id, null, null, null, null);

        var newLifecycle = new ProjectLifecycleFaker().AsActiveWithPhases(
            ("Discovery", "Discovery phase"),
            ("Build", "Build phase"),
            ("Launch", "Launch phase"));

        var newLifecyclePhases = newLifecycle.Phases.OrderBy(p => p.Order).ToList();

        var phaseMapping = new Dictionary<Guid, Guid>
        {
            { oldPhase1.Id, newLifecyclePhases[0].Id },
            { oldPhase2.Id, newLifecyclePhases[1].Id },
        };

        // Act
        var result = project.ChangeLifecycle(newLifecycle, phaseMapping);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.ProjectLifecycleId.Should().Be(newLifecycle.Id);
        project.Phases.Should().HaveCount(3);
        project.Phases.Select(p => p.Name).Should().BeEquivalentTo("Discovery", "Build", "Launch");

        var tasks = project.Tasks.ToList();
        tasks.Should().HaveCount(2);

        // Task 1 should be in the Discovery phase (mapped from Plan)
        var newDiscoveryPhase = project.Phases.First(p => p.Name == "Discovery");
        tasks.First(t => t.Name == "Task 1").ProjectPhaseId.Should().Be(newDiscoveryPhase.Id);

        // Task 2 should be in the Build phase (mapped from Execute)
        var newBuildPhase = project.Phases.First(p => p.Name == "Build");
        tasks.First(t => t.Name == "Task 2").ProjectPhaseId.Should().Be(newBuildPhase.Id);
    }

    [Fact]
    public void ChangeLifecycle_ShouldFail_WhenProjectIsClosed()
    {
        // Arrange
        var project = _projectFaker.AsCompleted(_dateTimeProvider);
        var newLifecycle = new ProjectLifecycleFaker().AsActiveWithPhases(("Phase 1", "First phase"));

        // Act
        var result = project.ChangeLifecycle(newLifecycle, []);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("closed");
    }

    [Fact]
    public void ChangeLifecycle_ShouldFail_WhenNoLifecycleAssigned()
    {
        // Arrange
        var project = _projectFaker.Generate();
        var newLifecycle = new ProjectLifecycleFaker().AsActiveWithPhases(("Phase 1", "First phase"));

        // Act
        var result = project.ChangeLifecycle(newLifecycle, []);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No lifecycle is currently assigned");
    }

    [Fact]
    public void ChangeLifecycle_ShouldFail_WhenNewLifecycleIsNotActive()
    {
        // Arrange
        var (project, _) = CreateProjectWithLifecycle(("Plan", "Planning phase"));
        var newLifecycle = new ProjectLifecycleFaker().AsProposedWithPhases(("Phase 1", "First phase"));

        // Act
        var result = project.ChangeLifecycle(newLifecycle, []);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("active");
    }

    [Fact]
    public void ChangeLifecycle_ShouldFail_WhenSameLifecycle()
    {
        // Arrange
        var lifecycle = new ProjectLifecycleFaker().AsActiveWithPhases(("Plan", "Planning phase"), ("Execute", "Execution phase"));
        var project = _projectFaker.Generate();
        project.AssignLifecycle(lifecycle);

        // Act
        var result = project.ChangeLifecycle(lifecycle, []);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("different");
    }

    [Fact]
    public void ChangeLifecycle_ShouldFail_WhenPhaseWithTasksNotMapped()
    {
        // Arrange
        var (project, oldPhases) = CreateProjectWithLifecycle(
            ("Plan", "Planning phase"),
            ("Execute", "Execution phase"));

        // Create a task in the Execute phase
        project.CreateTask(1, "Task 1", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, oldPhases[1].Id, null, null, null, null);

        var newLifecycle = new ProjectLifecycleFaker().AsActiveWithPhases(("Phase 1", "First phase"));
        var newPhases = newLifecycle.Phases.ToList();

        // Only map Plan, but Execute has tasks
        var phaseMapping = new Dictionary<Guid, Guid>
        {
            { oldPhases[0].Id, newPhases[0].Id },
        };

        // Act
        var result = project.ChangeLifecycle(newLifecycle, phaseMapping);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Execute");
    }

    [Fact]
    public void ChangeLifecycle_ShouldFail_WhenMappingTargetInvalid()
    {
        // Arrange
        var (project, oldPhases) = CreateProjectWithLifecycle(("Plan", "Planning phase"));
        project.CreateTask(1, "Task 1", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, oldPhases[0].Id, null, null, null, null);

        var newLifecycle = new ProjectLifecycleFaker().AsActiveWithPhases(("Phase 1", "First phase"));

        var phaseMapping = new Dictionary<Guid, Guid>
        {
            { oldPhases[0].Id, Guid.NewGuid() }, // Invalid target
        };

        // Act
        var result = project.ChangeLifecycle(newLifecycle, phaseMapping);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("does not exist");
    }

    [Fact]
    public void ChangeLifecycle_ShouldSucceed_WithEmptyPhasesNoTasks()
    {
        // Arrange
        var (project, _) = CreateProjectWithLifecycle(("Plan", "Planning phase"), ("Execute", "Execution phase"));

        var newLifecycle = new ProjectLifecycleFaker().AsActiveWithPhases(
            ("Discovery", "Discovery phase"),
            ("Build", "Build phase"));

        // No tasks, so no mapping needed for phases with tasks
        var phaseMapping = new Dictionary<Guid, Guid>();

        // Act
        var result = project.ChangeLifecycle(newLifecycle, phaseMapping);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.ProjectLifecycleId.Should().Be(newLifecycle.Id);
        project.Phases.Should().HaveCount(2);
        project.Phases.Select(p => p.Name).Should().BeEquivalentTo("Discovery", "Build");
    }

    [Fact]
    public void ChangeLifecycle_ShouldMapMultipleTasksToSamePhase()
    {
        // Arrange
        var (project, oldPhases) = CreateProjectWithLifecycle(
            ("Plan", "Planning phase"),
            ("Execute", "Execution phase"),
            ("Deliver", "Delivery phase"));

        project.CreateTask(1, "Task A", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, oldPhases[0].Id, null, null, null, null);
        project.CreateTask(2, "Task B", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, oldPhases[1].Id, null, null, null, null);
        project.CreateTask(3, "Task C", null, ProjectTaskType.Task, Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, oldPhases[2].Id, null, null, null, null);

        var newLifecycle = new ProjectLifecycleFaker().AsActiveWithPhases(("Single Phase", "The only phase"));
        var newPhases = newLifecycle.Phases.ToList();

        // Map all old phases to the single new phase
        var phaseMapping = new Dictionary<Guid, Guid>
        {
            { oldPhases[0].Id, newPhases[0].Id },
            { oldPhases[1].Id, newPhases[0].Id },
            { oldPhases[2].Id, newPhases[0].Id },
        };

        // Act
        var result = project.ChangeLifecycle(newLifecycle, phaseMapping);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Phases.Should().HaveCount(1);

        var singlePhase = project.Phases.First();
        project.Tasks.Should().OnlyContain(t => t.ProjectPhaseId == singlePhase.Id);
    }

    #endregion ChangeLifecycle Tests

    #region Scoring
    // A simple free-numeric model: Score = BV / JS. Lets tests pick exact rating values and assert on
    // the arithmetic without resolving scale levels.
    private ScoringModel FreeNumericModel() =>
        _scoringModelFaker.AsActiveWith(
            scales: [],
            criteria:
            [
                ("Business Value", "BV", null, null),
                ("Job Size", "JS", null, null),
            ],
            outputs:
            [
                ("Score", "Score", "BV / JS", true),
            ]);

    private (Project Project, Guid ActorId) ProjectWithOwner()
    {
        var actorId = Guid.NewGuid();
        var project = _projectFaker
            .WithData(roles: new() { [ProjectRole.Owner] = [actorId] })
            .Generate();
        return (project, actorId);
    }

    private static IReadOnlyDictionary<Guid, decimal> RatingsByToken(
        ScoringModel model,
        params (string Token, decimal Value)[] values)
    {
        var byToken = values.ToDictionary(v => v.Token, v => v.Value);
        return model.Criteria.ToDictionary(c => c.Id, c => byToken[c.Token]);
    }

    [Fact]
    public void RecordScore_WhenAuthorizedAndValid_ComputesPrimaryAndAppendsSnapshot()
    {
        // Arrange
        var now = _dateTimeProvider.Now;
        var (project, actorId) = ProjectWithOwner();
        var model = FreeNumericModel();
        var ratings = RatingsByToken(model, ("BV", 10m), ("JS", 2m));

        // Act
        var result = project.RecordScore(model, ratings, null, actorId, [], null, now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PrimaryValue.Should().Be(5m); // 10 / 2
        result.Value.ProjectId.Should().Be(project.Id);
        result.Value.ScoringModelId.Should().Be(model.Id);
        result.Value.ScoringModelName.Should().Be(model.Name);
        result.Value.ScoredById.Should().Be(actorId);
        result.Value.ScoredOn.Should().Be(now);
        result.Value.Sequence.Should().Be(1);
        project.Scores.Should().ContainSingle().Which.Should().Be(result.Value);
        project.LatestScore.Should().Be(result.Value);
        project.CurrentScore.Should().NotBeNull();
        project.CurrentScore!.Value.Should().Be(5m);
        project.CurrentScore.ScoredOn.Should().Be(now);
        project.CurrentScore.ScoredById.Should().Be(actorId);
        project.CurrentScore.ScoringModelName.Should().Be(model.Name);
    }

    [Fact]
    public void RecordScore_SnapshotsCriteriaAndOutputsFromModel()
    {
        // Arrange
        var now = _dateTimeProvider.Now;
        var (project, actorId) = ProjectWithOwner();
        var model = FreeNumericModel();
        var ratings = RatingsByToken(model, ("BV", 8m), ("JS", 4m));

        // Act
        var result = project.RecordScore(model, ratings, null, actorId, [], null, now);

        // Assert
        var score = result.Value;
        score.Ratings.Should().HaveCount(2);
        score.Ratings.Select(r => r.CriterionToken).Should().BeEquivalentTo(["BV", "JS"]);
        score.Ratings.Single(r => r.CriterionToken == "BV").RatingValue.Should().Be(8m);

        var output = score.Outputs.Should().ContainSingle().Subject;
        output.Token.Should().Be("Score");
        output.IsPrimary.Should().BeTrue();
        output.Value.Should().Be(2m); // 8 / 4
    }

    [Fact]
    public void RecordScore_WhenScaleBased_CapturesSelectedLevelLabel()
    {
        // Arrange — the WSJF fixture rates all criteria on the "Impact" scale (High=8, Medium=5, Low=1).
        var now = _dateTimeProvider.Now;
        var (project, actorId) = ProjectWithOwner();
        var model = _scoringModelFaker.AsActiveWsjf();
        var scale = model.Scales.Single();
        var high = scale.Levels.Single(l => l.Label == "High");

        var ratings = model.Criteria.ToDictionary(c => c.Id, _ => high.Value);
        var levels = model.Criteria.ToDictionary(
            c => c.Id,
            _ => (high.Id, high.Label));

        // Act
        var result = project.RecordScore(model, ratings, levels, actorId, [], null, now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Ratings.Should().OnlyContain(r => r.RatingLevelId == high.Id);
        result.Value.Ratings.Should().OnlyContain(r => r.RatingLevelLabel == "High");
        result.Value.Ratings.Should().OnlyContain(r => r.RatingValue == 8m);
        // CoD = BV+TC+RR = 24; WSJF = CoD / JS = 24 / 8 = 3.
        result.Value.PrimaryValue.Should().Be(3m);
        result.Value.Outputs.Should().HaveCount(2);
    }

    [Fact]
    public void RecordScore_WhenScoredMultipleTimes_IncrementsSequenceAndUpdatesCurrent()
    {
        // Arrange
        var now = _dateTimeProvider.Now;
        var (project, actorId) = ProjectWithOwner();
        var model = FreeNumericModel();

        // Act
        var first = project.RecordScore(model, RatingsByToken(model, ("BV", 10m), ("JS", 2m)), null, actorId, [], null, now);
        var second = project.RecordScore(model, RatingsByToken(model, ("BV", 9m), ("JS", 3m)), null, actorId, [], null, now.Plus(Duration.FromDays(1)));

        // Assert
        first.Value.Sequence.Should().Be(1);
        second.Value.Sequence.Should().Be(2);
        project.Scores.Should().HaveCount(2);
        project.LatestScore.Should().Be(second.Value);
        project.CurrentScore!.Value.Should().Be(3m); // 9 / 3
    }

    [Fact]
    public void RecordScore_WhenCalculationFails_ReturnsFailureAndDoesNotAppend()
    {
        // Arrange
        var now = _dateTimeProvider.Now;
        var (project, actorId) = ProjectWithOwner();
        var model = FreeNumericModel();
        var ratings = RatingsByToken(model, ("BV", 10m), ("JS", 0m)); // division by zero

        // Act
        var result = project.RecordScore(model, ratings, null, actorId, [], null, now);

        // Assert
        result.IsFailure.Should().BeTrue();
        project.Scores.Should().BeEmpty();
        project.CurrentScore.Should().BeNull();
    }

    [Fact]
    public void RecordScore_WhenActorNotAuthorized_ReturnsFailureAndDoesNotAppend()
    {
        // Arrange
        var now = _dateTimeProvider.Now;
        var project = _projectFaker.Generate();
        var model = FreeNumericModel();
        var ratings = RatingsByToken(model, ("BV", 10m), ("JS", 2m));
        var unauthorizedActor = Guid.NewGuid();

        // Act
        var result = project.RecordScore(model, ratings, null, unauthorizedActor, [], null, now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("owner or manager");
        project.Scores.Should().BeEmpty();
    }

    [Fact]
    public void RecordScore_WhenAuthorizedViaPortfolioOwner_Succeeds()
    {
        // Arrange
        var now = _dateTimeProvider.Now;
        var actorId = Guid.NewGuid();
        var project = _projectFaker.Generate();
        var model = FreeNumericModel();
        var ratings = RatingsByToken(model, ("BV", 6m), ("JS", 2m));
        var portfolioRoles = new[]
        {
            new RoleAssignment<ProjectPortfolioRole>(project.PortfolioId, ProjectPortfolioRole.Owner, actorId),
        };

        // Act
        var result = project.RecordScore(model, ratings, null, actorId, portfolioRoles, null, now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ScoredById.Should().Be(actorId);
    }

    [Fact]
    public void RecordScore_WhenAuthorizedViaProgramManager_Succeeds()
    {
        // Arrange
        var now = _dateTimeProvider.Now;
        var actorId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var project = _projectFaker.WithData(programId: programId).Generate();
        var model = FreeNumericModel();
        var ratings = RatingsByToken(model, ("BV", 6m), ("JS", 2m));
        var programRoles = new[]
        {
            new RoleAssignment<ProgramRole>(programId, ProgramRole.Manager, actorId),
        };

        // Act
        var result = project.RecordScore(model, ratings, null, actorId, [], programRoles, now);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void RecordScore_WhenSponsor_ReturnsFailure()
    {
        // Arrange
        var now = _dateTimeProvider.Now;
        var sponsorId = Guid.NewGuid();
        var project = _projectFaker
            .WithData(roles: new() { [ProjectRole.Sponsor] = [sponsorId] })
            .Generate();
        var model = FreeNumericModel();
        var ratings = RatingsByToken(model, ("BV", 6m), ("JS", 2m));

        // Act
        var result = project.RecordScore(model, ratings, null, sponsorId, [], null, now);

        // Assert
        result.IsFailure.Should().BeTrue();
        project.Scores.Should().BeEmpty();
    }

    #endregion Scoring
}
