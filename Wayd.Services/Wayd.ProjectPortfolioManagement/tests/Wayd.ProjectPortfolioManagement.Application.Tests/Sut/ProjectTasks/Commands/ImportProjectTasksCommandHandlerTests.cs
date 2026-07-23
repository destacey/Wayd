using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using NodaTime.Testing;
using NodaTime.Extensions;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Commands;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.ProjectTasks.Commands;

public class ImportProjectTasksCommandHandlerTests : IDisposable
{
    private const string ProjectKey = "APOLLO";
    private const string PhaseName = "Build";

    private static readonly LocalDate _start = new(2024, 7, 1);
    private static readonly LocalDate _end = new(2025, 6, 30);

    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly ImportProjectTasksCommandHandler _handler;
    private readonly Mock<ILogger<ImportProjectTasksCommandHandler>> _mockLogger;
    private readonly TestingDateTimeProvider _dateTimeProvider;

    private readonly Project _project;

    public ImportProjectTasksCommandHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _mockLogger = new Mock<ILogger<ImportProjectTasksCommandHandler>>();
        _dateTimeProvider = new TestingDateTimeProvider(new FakeClock(DateTime.UtcNow.ToInstant()));

        _handler = new ImportProjectTasksCommandHandler(_dbContext, _mockLogger.Object);

        // Tasks need a project with an assigned lifecycle, since phases come from it.
        var portfolio = ProjectPortfolio.Create("Growth", "Growth portfolio");
        portfolio.Activate(_start);

        _project = portfolio.CreateProject(
            "Project Apollo",
            "Apollo description",
            new ProjectKey(ProjectKey),
            1,
            new LocalDateRange(_start, _end),
            null,
            null,
            null,
            null,
            null,
            _dateTimeProvider.Now).Value;

        var lifecycle = new ProjectLifecycleFaker().WithName("Standard").AsActiveWithPhases((PhaseName, "Delivery"), ("Close", "Closure"));
        _project.AssignLifecycle(lifecycle);

        _dbContext.AddProject(_project);
    }

    [Fact]
    public async Task Handle_ImportsRootTask_IntoThePhaseNamedOnTheRow()
    {
        // Arrange
        var command = new ImportProjectTasksCommand([TaskRow("Design")]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var task = _project.Tasks.Single();
        task.Name.Should().Be("Design");
        task.ParentId.Should().BeNull();
        task.ProjectPhaseId.Should().Be(_project.Phases.Single(p => p.Name == PhaseName).Id);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_NestsTaskUnderItsNamedParent()
    {
        // Arrange
        var command = new ImportProjectTasksCommand([
            TaskRow("Design"),
            TaskRow("Wireframes", parentTaskName: "Design"),
        ]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var parent = _project.Tasks.Single(t => t.Name == "Design");
        var child = _project.Tasks.Single(t => t.Name == "Wireframes");
        child.ParentId.Should().Be(parent.Id);
    }

    [Fact]
    public async Task Handle_AppliesParentsBeforeChildren_WhenRowsAreOutOfOrder()
    {
        // Arrange
        // A file should not have to be pre-sorted: the grandchild is listed first here.
        var command = new ImportProjectTasksCommand([
            TaskRow("Mockups", parentTaskName: "Wireframes"),
            TaskRow("Wireframes", parentTaskName: "Design"),
            TaskRow("Design"),
        ]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var design = _project.Tasks.Single(t => t.Name == "Design");
        var wireframes = _project.Tasks.Single(t => t.Name == "Wireframes");
        var mockups = _project.Tasks.Single(t => t.Name == "Mockups");
        design.ParentId.Should().BeNull();
        wireframes.ParentId.Should().Be(design.Id);
        mockups.ParentId.Should().Be(wireframes.Id);
    }

    [Fact]
    public async Task Handle_Fails_WhenRowsFormAParentCycle()
    {
        // Arrange
        var command = new ImportProjectTasksCommand([
            TaskRow("Design", parentTaskName: "Wireframes"),
            TaskRow("Wireframes", parentTaskName: "Design"),
        ]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cycle");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ImportsMilestone_WithASinglePlannedDate()
    {
        // Arrange
        var row = TaskRow("Launch") with
        {
            Type = ProjectTaskType.Milestone,
            Progress = null,
            PlannedStart = null,
            PlannedEnd = null,
            PlannedDate = _end,
        };

        // Act
        var result = await _handler.Handle(new ImportProjectTasksCommand([row]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var milestone = _project.Tasks.Single();
        milestone.Type.Should().Be(ProjectTaskType.Milestone);
        milestone.PlannedDate.Should().Be(_end);
    }

    [Fact]
    public async Task Handle_NumbersTaskKeysSequentially_WithinTheProject()
    {
        // Arrange
        // The single-task handler takes a row lock for the next number; an import advances it in memory.
        var command = new ImportProjectTasksCommand([
            TaskRow("Design"),
            TaskRow("Build"),
            TaskRow("Test"),
        ]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _project.Tasks.Select(t => t.Key.Value)
            .Should().BeEquivalentTo(["APOLLO-1", "APOLLO-2", "APOLLO-3"]);
    }

    [Fact]
    public async Task Handle_AssignsAssignees_ResolvedByEmployeeNumber()
    {
        // Arrange
        var assignee = new EmployeeFaker().WithEmployeeNumber("E100").Generate();
        _dbContext.AddEmployee(assignee);

        var row = TaskRow("Design") with { AssigneeEmployeeNumbers = ["E100"] };

        // Act
        var result = await _handler.Handle(new ImportProjectTasksCommand([row]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _project.Tasks.Single().Roles
            .Should().ContainSingle(r => r.Role == TaskRole.Assignee && r.EmployeeId == assignee.Id);
    }

    [Fact]
    public async Task Handle_Fails_WhenThePhaseIsNotOnTheProjectsLifecycle()
    {
        // Arrange
        var row = TaskRow("Design") with { PhaseName = "Nonexistent" };

        // Act
        var result = await _handler.Handle(new ImportProjectTasksCommand([row]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Nonexistent");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Fails_WhenTheParentTaskCannotBeResolved()
    {
        // Arrange
        var row = TaskRow("Wireframes", parentTaskName: "Nonexistent");

        // Act
        var result = await _handler.Handle(new ImportProjectTasksCommand([row]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Nonexistent");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Fails_WhenTheProjectCannotBeResolved()
    {
        // Arrange
        var row = TaskRow("Design") with { ProjectKey = new ProjectKey("GEMINI") };

        // Act
        var result = await _handler.Handle(new ImportProjectTasksCommand([row]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("GEMINI");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Fails_WhenATaskNameIsDuplicatedWithinTheProject()
    {
        // Arrange
        // Names are how child rows point at parents, so a duplicate would be ambiguous.
        var command = new ImportProjectTasksCommand([TaskRow("Design"), TaskRow("design")]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("more than once");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    private static ImportProjectTaskDto TaskRow(string name, string? parentTaskName = null) =>
        new(
            new ProjectKey(ProjectKey),
            name,
            $"{name} description",
            ProjectTaskType.Task,
            TaskStatus.NotStarted,
            TaskPriority.Medium,
            PhaseName,
            parentTaskName,
            0m,
            _start,
            _end,
            null,
            null,
            []);

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
