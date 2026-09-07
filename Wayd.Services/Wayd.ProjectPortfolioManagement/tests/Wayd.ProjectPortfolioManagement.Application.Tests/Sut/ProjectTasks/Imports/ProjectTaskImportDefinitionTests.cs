using FluentAssertions;
using NodaTime;
using NodaTime.Testing;
using NodaTime.Extensions;
using Wayd.Common.Application.Imports;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Imports;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Dtos;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Imports;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.ProjectTasks.Imports;

public sealed class ProjectTaskImportDefinitionTests : IDisposable
{
    private const int CreatePass = 0;
    private const string ProjectKeyValue = "APOLLO";
    private const string StageName = "Build";

    private static readonly LocalDate _start = new(2024, 7, 1);
    private static readonly LocalDate _end = new(2025, 6, 30);

    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly ProjectTaskImportDefinition _definition;

    private readonly Project _project;

    public ProjectTaskImportDefinitionTests()
    {
        var clock = new TestingDateTimeProvider(new FakeClock(DateTime.UtcNow.ToInstant()));
        _definition = new ProjectTaskImportDefinition(_dbContext, new ImportPayloadSerializer());

        // Tasks need a project with an assigned lifecycle, since stages come from it.
        var portfolio = ProjectPortfolio.Create("Growth", "Growth portfolio");
        portfolio.Activate(PpmActor.System, _start);

        _project = portfolio.CreateProject(
            "Project Apollo",
            "Apollo description",
            new ProjectKey(ProjectKeyValue),
            1,
            new LocalDateRange(_start, _end),
            null,
            null,
            null,
            null,
            null,
            clock.Now, PpmActor.System).Value;

        var lifecycle = new ProjectLifecycleFaker().WithName("Standard").AsActiveWithStages((StageName, "Delivery"), ("Close", "Closure"));
        _project.AssignLifecycle(PpmActor.System, ProjectAncestryRoles.None, lifecycle);

        _dbContext.AddProject(_project);
    }

    public void Dispose() => _dbContext.Dispose();

    /// <summary>Rows keyed r1, r2, … so a child can name its parent's import id.</summary>
    private ImportProcessRow[] Rows(params ImportProjectTaskDto[] tasks) =>
        [.. tasks.Select((t, i) => ImportProcessRow.Create($"r{i + 1}", i + 1, _definition.SerializeRow(t)))];

    private Task<CSharpFunctionalExtensions.Result<ImportPassResult>> Run(params ImportProjectTaskDto[] tasks) =>
        _definition.ExecutePass(
            Guid.CreateVersion7(), CreatePass, Rows(tasks), isFinalChunk: true, TestContext.Current.CancellationToken);

    [Fact]
    public void Definition_IsAtomicAndCannotBeChunked()
    {
        // Arrange & Act & Assert — a child row hangs off a parent row in the same file and the task number
        // sequence advances as rows are applied, so a chunk could be handed a child with no parent
        _definition.Atomicity.Should().Be(ImportAtomicity.Atomic);
        _definition.Passes.Single().Scope.Should().Be(ImportPassScope.WholeSet);
    }

    [Fact]
    public async Task CreateTasks_PutsARootTaskInTheStageTheRowNames()
    {
        // Arrange & Act
        var result = await Run(TaskRow("Design"));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeFalse();

        var task = _project.Tasks.Single();
        task.Name.Should().Be("Design");
        task.ParentId.Should().BeNull();
        task.ProjectStageId.Should().Be(_project.Stages.Single(s => s.Name == StageName).Id);
        outcome.CreatedEntityId.Should().Be(task.Id);
    }

    [Fact]
    public async Task CreateTasks_NestsATaskUnderTheRowItNames()
    {
        // Arrange & Act — the child names its parent's import id, which is r1 here
        var result = await Run(
            TaskRow("Design"),
            TaskRow("Wireframes", parentImportId: "r1"));

        // Assert
        result.Value.Rows.Should().AllSatisfy(r => r.Failed.Should().BeFalse());

        var parent = _project.Tasks.Single(t => t.Name == "Design");
        _project.Tasks.Single(t => t.Name == "Wireframes").ParentId.Should().Be(parent.Id);
    }

    [Fact]
    public async Task CreateTasks_AppliesParentsBeforeChildrenWhenRowsAreOutOfOrder()
    {
        // Arrange & Act — a file should not have to be pre-sorted: the grandchild is listed first
        var result = await Run(
            TaskRow("Mockups", parentImportId: "r2"),
            TaskRow("Wireframes", parentImportId: "r3"),
            TaskRow("Design"));

        // Assert
        result.Value.Rows.Should().AllSatisfy(r => r.Failed.Should().BeFalse());

        var design = _project.Tasks.Single(t => t.Name == "Design");
        var wireframes = _project.Tasks.Single(t => t.Name == "Wireframes");
        var mockups = _project.Tasks.Single(t => t.Name == "Mockups");
        design.ParentId.Should().BeNull();
        wireframes.ParentId.Should().Be(design.Id);
        mockups.ParentId.Should().Be(wireframes.Id);
    }

    [Fact]
    public async Task CreateTasks_RejectsRowsThatFormAParentCycle()
    {
        // Arrange & Act — the submission command rejects a cycle before the run starts; if one reaches the
        // pass, the rows involved can never be ordered parents-first, so each is rejected on its own
        var result = await Run(
            TaskRow("Design", parentImportId: "r2"),
            TaskRow("Wireframes", parentImportId: "r1"));

        // Assert
        result.Value.Rows.Should().AllSatisfy(r => r.Failed.Should().BeTrue());
        _project.Tasks.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateTasks_NestsATaskUnderAnExistingTaskById()
    {
        // Arrange — a parent that is not in the file is named by its id instead
        await Run(TaskRow("Design"));
        var existing = _project.Tasks.Single(t => t.Name == "Design");

        // Act
        var result = await Run(TaskRow("Wireframes") with { ParentTaskId = existing.Id });

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _project.Tasks.Single(t => t.Name == "Wireframes").ParentId.Should().Be(existing.Id);
    }

    [Fact]
    public async Task CreateTasks_CreatesAMilestoneWithASinglePlannedDate()
    {
        // Arrange & Act
        var result = await Run(TaskRow("Launch") with
        {
            Type = ProjectTaskType.Milestone,
            Progress = null,
            PlannedStart = null,
            PlannedEnd = null,
            PlannedDate = _end,
        });

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();

        var milestone = _project.Tasks.Single();
        milestone.Type.Should().Be(ProjectTaskType.Milestone);
        milestone.PlannedDate.Should().Be(_end);
    }

    [Fact]
    public async Task CreateTasks_NumbersTaskKeysSequentiallyWithinTheProject()
    {
        // Arrange & Act — the single-task handler takes a row lock for the next number; a run advances it
        // in memory instead
        var result = await Run(TaskRow("Design"), TaskRow("Build"), TaskRow("Test"));

        // Assert
        result.Value.Rows.Should().AllSatisfy(r => r.Failed.Should().BeFalse());
        _project.Tasks.Select(t => t.Key.Value).Should().BeEquivalentTo(["APOLLO-1", "APOLLO-2", "APOLLO-3"]);
    }

    [Fact]
    public async Task CreateTasks_AssignsAssigneesResolvedByEmployeeNumber()
    {
        // Arrange
        var assignee = new EmployeeFaker().WithEmployeeNumber("E100").Generate();
        _dbContext.AddEmployee(assignee);

        // Act
        var result = await Run(TaskRow("Design") with { AssigneeEmployeeNumbers = ["E100"] });

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _project.Tasks.Single().Roles
            .Should().ContainSingle(r => r.Role == TaskRole.Assignee && r.EmployeeId == assignee.Id);
    }

    [Fact]
    public async Task CreateTasks_RejectsARowNamingAStageTheLifecycleDoesNotDefine()
    {
        // Arrange & Act
        var result = await Run(TaskRow("Design") with { StageName = "Nonexistent" });

        // Assert — named per row, so the person knows which line to fix
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("Nonexistent");
    }

    [Fact]
    public async Task CreateTasks_RejectsARowNamingAParentImportIdThatIsNotInTheFile()
    {
        // Arrange & Act
        var result = await Run(TaskRow("Wireframes", parentImportId: "nope"));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("nope");
    }

    [Fact]
    public async Task CreateTasks_RejectsARowNamingAnEmployeeThatDoesNotExist()
    {
        // Arrange & Act
        var result = await Run(TaskRow("Design") with { AssigneeEmployeeNumbers = ["MISSING"] });

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("MISSING");
    }

    [Fact]
    public async Task CreateTasks_RejectsARowNamingAProjectThatDoesNotExist()
    {
        // Arrange & Act
        var result = await Run(TaskRow("Design") with { ProjectKey = new ProjectKey("GEMINI") });

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("GEMINI");
    }

    private static ImportProjectTaskDto TaskRow(string name, string? parentImportId = null) =>
        new(
            new ProjectKey(ProjectKeyValue),
            name,
            $"{name} description",
            ProjectTaskType.Task,
            TaskStatus.NotStarted,
            TaskPriority.Medium,
            StageName,
            parentImportId,
            null,
            0m,
            _start,
            _end,
            null,
            null,
            []);
}
