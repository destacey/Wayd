using FluentAssertions;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Services;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Sut.Services;

public class WbsCalculatorTests
{
    private static ProjectTaskFaker TaskFaker() => new();
    private static ProjectPhaseFaker PhaseFaker() => new();

    #region CalculateWbs - Without Phases

    [Fact]
    public void CalculateWbs_ShouldReturnPosition_ForSingleRootTask()
    {
        // Arrange
        var task = TaskFaker().WithOrder(1).Generate();
        var tasks = new List<ProjectTask> { task };

        // Act
        var wbs = WbsCalculator.CalculateWbs(task, tasks);

        // Assert
        wbs.Should().Be("1");
    }

    [Fact]
    public void CalculateWbs_ShouldReturnCorrectPosition_ForMultipleRootTasks()
    {
        // Arrange
        var task1 = TaskFaker().WithOrder(1).Generate();
        var task2 = TaskFaker().WithOrder(2).Generate();
        var task3 = TaskFaker().WithOrder(3).Generate();
        var tasks = new List<ProjectTask> { task1, task2, task3 };

        // Act & Assert
        WbsCalculator.CalculateWbs(task1, tasks).Should().Be("1");
        WbsCalculator.CalculateWbs(task2, tasks).Should().Be("2");
        WbsCalculator.CalculateWbs(task3, tasks).Should().Be("3");
    }

    [Fact]
    public void CalculateWbs_ShouldReturnHierarchicalCode_ForChildTasks()
    {
        // Arrange
        var parentTask = TaskFaker().WithOrder(1).Generate();
        var childTask = TaskFaker().WithOrder(1).WithParentId(parentTask.Id).Generate();
        var tasks = new List<ProjectTask> { parentTask, childTask };

        // Act
        var wbs = WbsCalculator.CalculateWbs(childTask, tasks);

        // Assert
        wbs.Should().Be("1.1");
    }

    [Fact]
    public void CalculateWbs_ShouldReturnCorrectCodes_ForMultipleChildTasks()
    {
        // Arrange
        var parentTask = TaskFaker().WithOrder(1).Generate();
        var child1 = TaskFaker().WithOrder(1).WithParentId(parentTask.Id).Generate();
        var child2 = TaskFaker().WithOrder(2).WithParentId(parentTask.Id).Generate();
        var child3 = TaskFaker().WithOrder(3).WithParentId(parentTask.Id).Generate();
        var tasks = new List<ProjectTask> { parentTask, child1, child2, child3 };

        // Act & Assert
        WbsCalculator.CalculateWbs(child1, tasks).Should().Be("1.1");
        WbsCalculator.CalculateWbs(child2, tasks).Should().Be("1.2");
        WbsCalculator.CalculateWbs(child3, tasks).Should().Be("1.3");
    }

    [Fact]
    public void CalculateWbs_ShouldReturnDeepHierarchicalCode_ForNestedTasks()
    {
        // Arrange
        var root = TaskFaker().WithOrder(2).Generate();
        var child = TaskFaker().WithOrder(3).WithParentId(root.Id).Generate();
        var grandchild = TaskFaker().WithOrder(1).WithParentId(child.Id).Generate();
        var tasks = new List<ProjectTask> { root, child, grandchild };

        // Act
        var wbs = WbsCalculator.CalculateWbs(grandchild, tasks);

        // Assert
        wbs.Should().Be("1.1.1");
    }

    [Fact]
    public void CalculateWbs_ShouldOrderByOrderProperty_NotByInsertionOrder()
    {
        // Arrange - add tasks in reverse order
        var task1 = TaskFaker().WithOrder(3).Generate();
        var task2 = TaskFaker().WithOrder(1).Generate();
        var task3 = TaskFaker().WithOrder(2).Generate();
        var tasks = new List<ProjectTask> { task1, task2, task3 };

        // Act & Assert
        WbsCalculator.CalculateWbs(task1, tasks).Should().Be("3");
        WbsCalculator.CalculateWbs(task2, tasks).Should().Be("1");
        WbsCalculator.CalculateWbs(task3, tasks).Should().Be("2");
    }

    [Fact]
    public void CalculateWbs_ShouldScopeSiblings_ToSameParent()
    {
        // Arrange - two parents each with children
        var parent1 = TaskFaker().WithOrder(1).Generate();
        var parent2 = TaskFaker().WithOrder(2).Generate();
        var child1OfParent1 = TaskFaker().WithOrder(1).WithParentId(parent1.Id).Generate();
        var child2OfParent1 = TaskFaker().WithOrder(2).WithParentId(parent1.Id).Generate();
        var child1OfParent2 = TaskFaker().WithOrder(1).WithParentId(parent2.Id).Generate();
        var tasks = new List<ProjectTask> { parent1, parent2, child1OfParent1, child2OfParent1, child1OfParent2 };

        // Act & Assert
        WbsCalculator.CalculateWbs(child1OfParent1, tasks).Should().Be("1.1");
        WbsCalculator.CalculateWbs(child2OfParent1, tasks).Should().Be("1.2");
        WbsCalculator.CalculateWbs(child1OfParent2, tasks).Should().Be("2.1");
    }

    #endregion

    #region CalculateWbs - With Phases

    [Fact]
    public void CalculateWbs_ShouldPrefixWithPhaseOrder_WhenPhasesProvided()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var phase1 = PhaseFaker().WithProjectId(projectId).WithOrder(1).Generate();
        var phase2 = PhaseFaker().WithProjectId(projectId).WithOrder(2).Generate();
        var phases = new List<ProjectPhase> { phase1, phase2 };

        var task = TaskFaker().WithOrder(1).WithProjectPhaseId(phase1.Id).Generate();
        var tasks = new List<ProjectTask> { task };

        // Act
        var wbs = WbsCalculator.CalculateWbs(task, tasks, phases);

        // Assert
        wbs.Should().Be("1.1");
    }

    [Fact]
    public void CalculateWbs_ShouldUseCorrectPhasePrefix_ForDifferentPhases()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var phase1 = PhaseFaker().WithProjectId(projectId).WithOrder(1).Generate();
        var phase2 = PhaseFaker().WithProjectId(projectId).WithOrder(2).Generate();
        var phase3 = PhaseFaker().WithProjectId(projectId).WithOrder(3).Generate();
        var phases = new List<ProjectPhase> { phase1, phase2, phase3 };

        var taskInPhase1 = TaskFaker().WithOrder(1).WithProjectPhaseId(phase1.Id).Generate();
        var taskInPhase3 = TaskFaker().WithOrder(1).WithProjectPhaseId(phase3.Id).Generate();
        var tasks = new List<ProjectTask> { taskInPhase1, taskInPhase3 };

        // Act & Assert
        WbsCalculator.CalculateWbs(taskInPhase1, tasks, phases).Should().Be("1.1");
        WbsCalculator.CalculateWbs(taskInPhase3, tasks, phases).Should().Be("3.1");
    }

    [Fact]
    public void CalculateWbs_ShouldScopeRootSiblings_ToSamePhase()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var phase1 = PhaseFaker().WithProjectId(projectId).WithOrder(1).Generate();
        var phase2 = PhaseFaker().WithProjectId(projectId).WithOrder(2).Generate();
        var phases = new List<ProjectPhase> { phase1, phase2 };

        var task1InPhase1 = TaskFaker().WithOrder(1).WithProjectPhaseId(phase1.Id).Generate();
        var task2InPhase1 = TaskFaker().WithOrder(2).WithProjectPhaseId(phase1.Id).Generate();
        var task1InPhase2 = TaskFaker().WithOrder(1).WithProjectPhaseId(phase2.Id).Generate();
        var tasks = new List<ProjectTask> { task1InPhase1, task2InPhase1, task1InPhase2 };

        // Act & Assert
        WbsCalculator.CalculateWbs(task1InPhase1, tasks, phases).Should().Be("1.1");
        WbsCalculator.CalculateWbs(task2InPhase1, tasks, phases).Should().Be("1.2");
        WbsCalculator.CalculateWbs(task1InPhase2, tasks, phases).Should().Be("2.1");
    }

    [Fact]
    public void CalculateWbs_ShouldIncludePhasePrefix_ForNestedTasks()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var phase = PhaseFaker().WithProjectId(projectId).WithOrder(3).Generate();
        var phases = new List<ProjectPhase> { phase };

        var rootTask = TaskFaker().WithOrder(1).WithProjectPhaseId(phase.Id).Generate();
        var childTask = TaskFaker().WithOrder(1).WithParentId(rootTask.Id).WithProjectPhaseId(phase.Id).Generate();
        var grandchild = TaskFaker().WithOrder(1).WithParentId(childTask.Id).WithProjectPhaseId(phase.Id).Generate();
        var tasks = new List<ProjectTask> { rootTask, childTask, grandchild };

        // Act & Assert
        WbsCalculator.CalculateWbs(rootTask, tasks, phases).Should().Be("3.1");
        WbsCalculator.CalculateWbs(childTask, tasks, phases).Should().Be("3.1.1");
        WbsCalculator.CalculateWbs(grandchild, tasks, phases).Should().Be("3.1.1.1");
    }

    [Fact]
    public void CalculateWbs_ShouldNotPrefixPhase_WhenPhasesNull()
    {
        // Arrange
        var task = TaskFaker().WithOrder(1).Generate();
        var tasks = new List<ProjectTask> { task };

        // Act
        var wbs = WbsCalculator.CalculateWbs(task, tasks, null);

        // Assert
        wbs.Should().Be("1");
    }

    #endregion

    #region CalculateAllWbs - Without Phases

    [Fact]
    public void CalculateAllWbs_ShouldReturnWbsForAllTasks()
    {
        // Arrange
        var parent = TaskFaker().WithOrder(1).Generate();
        var child1 = TaskFaker().WithOrder(1).WithParentId(parent.Id).Generate();
        var child2 = TaskFaker().WithOrder(2).WithParentId(parent.Id).Generate();
        var tasks = new List<ProjectTask> { parent, child1, child2 };

        // Act
        var result = WbsCalculator.CalculateAllWbs(tasks);

        // Assert
        result.Should().HaveCount(3);
        result[parent.Id].Should().Be("1");
        result[child1.Id].Should().Be("1.1");
        result[child2.Id].Should().Be("1.2");
    }

    [Fact]
    public void CalculateAllWbs_ShouldReturnEmptyDictionary_ForEmptyTaskList()
    {
        // Act
        var result = WbsCalculator.CalculateAllWbs(Enumerable.Empty<ProjectTask>());

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region CalculateAllWbs - With Phases

    [Fact]
    public void CalculateAllWbs_ShouldReturnPhasePrefixedWbs_ForAllTasks()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var phase1 = PhaseFaker().WithProjectId(projectId).WithOrder(1).Generate();
        var phase2 = PhaseFaker().WithProjectId(projectId).WithOrder(2).Generate();
        var phases = new List<ProjectPhase> { phase1, phase2 };

        var rootInPhase1 = TaskFaker().WithOrder(1).WithProjectPhaseId(phase1.Id).Generate();
        var childInPhase1 = TaskFaker().WithOrder(1).WithParentId(rootInPhase1.Id).WithProjectPhaseId(phase1.Id).Generate();
        var rootInPhase2 = TaskFaker().WithOrder(1).WithProjectPhaseId(phase2.Id).Generate();
        var tasks = new List<ProjectTask> { rootInPhase1, childInPhase1, rootInPhase2 };

        // Act
        var result = WbsCalculator.CalculateAllWbs(tasks, phases);

        // Assert
        result.Should().HaveCount(3);
        result[rootInPhase1.Id].Should().Be("1.1");
        result[childInPhase1.Id].Should().Be("1.1.1");
        result[rootInPhase2.Id].Should().Be("2.1");
    }

    [Fact]
    public void CalculateAllWbs_ShouldReturnNonPrefixedWbs_WhenPhasesNull()
    {
        // Arrange
        var task1 = TaskFaker().WithOrder(1).Generate();
        var task2 = TaskFaker().WithOrder(2).Generate();
        var tasks = new List<ProjectTask> { task1, task2 };

        // Act
        var result = WbsCalculator.CalculateAllWbs(tasks, null);

        // Assert
        result[task1.Id].Should().Be("1");
        result[task2.Id].Should().Be("2");
    }

    #endregion

    #region CalculateWbs - Edge Cases

    [Fact]
    public void CalculateWbs_ShouldHandleComplexHierarchy_AcrossMultiplePhases()
    {
        // Arrange - realistic project plan structure
        var projectId = Guid.NewGuid();
        var planning = PhaseFaker().WithProjectId(projectId).WithName("Planning").WithOrder(1).Generate();
        var execution = PhaseFaker().WithProjectId(projectId).WithName("Execution").WithOrder(2).Generate();
        var closure = PhaseFaker().WithProjectId(projectId).WithName("Closure").WithOrder(3).Generate();
        var phases = new List<ProjectPhase> { planning, execution, closure };

        // Planning phase tasks
        var requirements = TaskFaker().WithName("Requirements").WithOrder(1).WithProjectPhaseId(planning.Id).Generate();
        var design = TaskFaker().WithName("Design").WithOrder(2).WithProjectPhaseId(planning.Id).Generate();

        // Execution phase tasks with nesting
        var buildApi = TaskFaker().WithName("Build API").WithOrder(1).WithProjectPhaseId(execution.Id).Generate();
        var endpoint1 = TaskFaker().WithName("Users endpoint").WithOrder(1).WithParentId(buildApi.Id).WithProjectPhaseId(execution.Id).Generate();
        var endpoint2 = TaskFaker().WithName("Orders endpoint").WithOrder(2).WithParentId(buildApi.Id).WithProjectPhaseId(execution.Id).Generate();
        var buildUi = TaskFaker().WithName("Build UI").WithOrder(2).WithProjectPhaseId(execution.Id).Generate();

        // Closure phase tasks
        var signOff = TaskFaker().WithName("Sign-off").WithOrder(1).WithProjectPhaseId(closure.Id).Generate();

        var tasks = new List<ProjectTask> { requirements, design, buildApi, endpoint1, endpoint2, buildUi, signOff };

        // Act
        var result = WbsCalculator.CalculateAllWbs(tasks, phases);

        // Assert
        result[requirements.Id].Should().Be("1.1");      // Planning > Requirements
        result[design.Id].Should().Be("1.2");             // Planning > Design
        result[buildApi.Id].Should().Be("2.1");           // Execution > Build API
        result[endpoint1.Id].Should().Be("2.1.1");        // Execution > Build API > Users endpoint
        result[endpoint2.Id].Should().Be("2.1.2");        // Execution > Build API > Orders endpoint
        result[buildUi.Id].Should().Be("2.2");            // Execution > Build UI
        result[signOff.Id].Should().Be("3.1");            // Closure > Sign-off
    }

    [Fact]
    public void CalculateWbs_ShouldHandleSameOrderValues_Deterministically()
    {
        // Arrange - tasks with same order (edge case)
        var task1 = TaskFaker().WithOrder(1).Generate();
        var task2 = TaskFaker().WithOrder(1).Generate();
        var tasks = new List<ProjectTask> { task1, task2 };

        // Act
        var result = WbsCalculator.CalculateAllWbs(tasks);

        // Assert - both get calculated (positions based on iteration order among same-order tasks)
        result.Should().HaveCount(2);
        result.Values.Should().Contain("1");
        result.Values.Should().Contain("2");
    }

    #endregion
}