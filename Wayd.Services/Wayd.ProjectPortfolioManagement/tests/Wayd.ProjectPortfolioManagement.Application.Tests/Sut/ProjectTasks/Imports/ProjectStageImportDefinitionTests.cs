using FluentAssertions;
using NodaTime;
using NodaTime.Testing;
using NodaTime.Extensions;
using Wayd.Common.Application.Imports;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Imports;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Dtos;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Imports;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.ProjectTasks.Imports;

public sealed class ProjectStageImportDefinitionTests : IDisposable
{
    private const int UpdatePass = 0;
    private const string ProjectKeyValue = "APOLLO";
    private const string StageName = "Build";

    private static readonly LocalDate _start = new(2024, 7, 1);
    private static readonly LocalDate _end = new(2025, 6, 30);

    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly ProjectStageImportDefinition _definition;

    private readonly Project _project;

    public ProjectStageImportDefinitionTests()
    {
        var clock = new TestingDateTimeProvider(new FakeClock(DateTime.UtcNow.ToInstant()));
        _definition = new ProjectStageImportDefinition(_dbContext, new ImportPayloadSerializer());

        // A project with an assigned lifecycle, which is where its stages come from.
        var portfolio = ProjectPortfolio.Create(
            "Growth", "Growth portfolio", null, EventActor.System, clock.Now);
        portfolio.Activate(PpmActor.System, _start, clock.Now);

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
        _project.AssignLifecycle(PpmActor.System, ProjectAncestryRoles.None, lifecycle, clock.Now);

        _dbContext.AddProject(_project);
    }

    public void Dispose() => _dbContext.Dispose();

    private ImportProcessRow[] Rows(params ImportProjectStageDto[] stages) =>
        [.. stages.Select((s, i) => ImportProcessRow.Create($"r{i + 1}", i + 1, _definition.SerializeRow(s)))];

    private Task<CSharpFunctionalExtensions.Result<ImportPassResult>> Run(params ImportProjectStageDto[] stages) =>
        _definition.ExecutePass(
            Guid.CreateVersion7(), UpdatePass, Rows(stages), isFinalChunk: true, TestContext.Current.CancellationToken);

    [Fact]
    public void Definition_IsAtomicWithOnePass()
    {
        // Arrange & Act & Assert — a stage import corrects projects that already exist, so a file that
        // half applies leaves a project's stages disagreeing with each other
        _definition.Atomicity.Should().Be(ImportAtomicity.Atomic);
        _definition.Passes.Single().Name.Should().Be("UpdateStages");
    }

    [Fact]
    public async Task UpdateStages_SetsTheStatusExactlyAsGiven()
    {
        // Arrange & Act — the status is applied verbatim, not derived from the stage's tasks
        var result = await Run(Row(StageName, TaskStatus.Completed));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeFalse();

        var stage = _project.Stages.Single(s => s.Name == StageName);
        stage.Status.Should().Be(TaskStatus.Completed);
        outcome.CreatedEntityId.Should().Be(stage.Id);
    }

    [Fact]
    public async Task UpdateStages_SetsEachStageIndependently()
    {
        // Arrange & Act
        var result = await Run(
            Row("Build", TaskStatus.Completed),
            Row("Close", TaskStatus.InProgress));

        // Assert
        result.Value.Rows.Should().AllSatisfy(r => r.Failed.Should().BeFalse());
        _project.Stages.Single(s => s.Name == "Build").Status.Should().Be(TaskStatus.Completed);
        _project.Stages.Single(s => s.Name == "Close").Status.Should().Be(TaskStatus.InProgress);
    }

    [Fact]
    public async Task UpdateStages_RejectsARowNamingAProjectThatDoesNotExist()
    {
        // Arrange & Act
        var result = await Run(Row(StageName, TaskStatus.Completed) with { ProjectKey = new ProjectKey("GEMINI") });

        // Assert — named per row, so the person knows which line to fix
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("GEMINI");
    }

    [Fact]
    public async Task UpdateStages_RejectsARowNamingAStageTheLifecycleDoesNotDefine()
    {
        // Arrange & Act
        var result = await Run(Row("Nonexistent", TaskStatus.Completed));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("Nonexistent");
    }

    private static ImportProjectStageDto Row(string stageName, TaskStatus status) =>
        new(new ProjectKey(ProjectKeyValue), stageName, status);
}
