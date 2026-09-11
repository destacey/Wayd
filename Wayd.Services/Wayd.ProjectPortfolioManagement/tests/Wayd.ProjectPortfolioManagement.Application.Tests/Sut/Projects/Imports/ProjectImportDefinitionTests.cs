using FluentAssertions;
using NodaTime;
using NodaTime.Testing;
using NodaTime.Extensions;
using Wayd.Common.Application.Imports;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Imports;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Projects.Imports;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Imports;

public sealed class ProjectImportDefinitionTests : IDisposable
{
    private const int CreatePass = 0;

    private static readonly LocalDate _start = new(2024, 7, 1);
    private static readonly LocalDate _end = new(2025, 6, 30);

    // Deliberately all different, and all well before the run: a transition stamped with the wrong one of
    // them is then visible, which a shared date would hide.
    private static readonly LocalDate _created = new(2024, 3, 4);
    private static readonly LocalDate _activated = new(2024, 7, 8);
    private static readonly LocalDate _closed = new(2025, 5, 6);

    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly ProjectImportDefinition _definition;

    private readonly ProjectPortfolio _portfolio;
    private readonly int _categoryId;

    public ProjectImportDefinitionTests()
    {
        _dateTimeProvider = new TestingDateTimeProvider(new FakeClock(DateTime.UtcNow.ToInstant()));
        _definition = new ProjectImportDefinition(_dbContext, new ImportPayloadSerializer());

        // A project can only be created inside an active portfolio, so every case starts from one.
        _portfolio = ProjectPortfolio.Create(
            "Growth", "Growth portfolio", null, EventActor.System, _dateTimeProvider.Now);
        _portfolio.Activate(PpmActor.System, _start, _dateTimeProvider.Now);
        _dbContext.AddPortfolio(_portfolio);

        var category = new ExpenditureCategoryFaker().WithName("Capex").Generate();
        _dbContext.AddExpenditureCategory(category);
        _categoryId = category.Id;
    }

    public void Dispose() => _dbContext.Dispose();

    private ImportProcessRow[] Rows(params ImportProjectDto[] projects) =>
        [.. projects.Select((p, i) => ImportProcessRow.Create($"r{i + 1}", i + 1, _definition.SerializeRow(p)))];

    private Task<CSharpFunctionalExtensions.Result<ImportPassResult>> Run(params ImportProjectDto[] projects) =>
        _definition.ExecutePass(
            Guid.CreateVersion7(), CreatePass, Rows(projects), isFinalChunk: true, TestContext.Current.CancellationToken);

    [Fact]
    public void Definition_IsAtomicWithOnePass()
    {
        // Arrange & Act & Assert — projects are what tasks, stages and initiatives are imported against,
        // so a half-applied file leaves those imports resolving only some of the keys they reference
        _definition.Atomicity.Should().Be(ImportAtomicity.Atomic);
        _definition.Passes.Single().Name.Should().Be("CreateProjects");
    }

    [Fact]
    public async Task CreateProjects_CreatesTheProjectResolvingItsPortfolioAndCategoryById()
    {
        // Arrange & Act
        var result = await Run(Row("APOLLO", ProjectStatus.Proposed, start: null));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeFalse();

        var project = _portfolio.Projects.Single();
        project.Key.Value.Should().Be("APOLLO");
        project.Status.Should().Be(ProjectStatus.Proposed);
        project.PortfolioId.Should().Be(_portfolio.Id);
        outcome.CreatedEntityId.Should().Be(project.Id);
    }

    [Theory]
    [InlineData(ProjectStatus.Active)]
    [InlineData(ProjectStatus.Completed)]
    [InlineData(ProjectStatus.Canceled)]
    public async Task CreateProjects_DrivesTheProjectToItsTargetStatus(ProjectStatus status)
    {
        // Arrange & Act — unlike programs and portfolios, a project has nothing beneath it to close first,
        // so it reaches its true final status during the run
        var result = await Run(Row("APOLLO", status, _start, _end));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _portfolio.Projects.Single().Status.Should().Be(status);
    }

    [Fact]
    public async Task CreateProjects_AssignsTheLifecycleAndCopiesItsStages()
    {
        // Arrange — the lifecycle's stages are copied into project stages, which the task import lands into
        var lifecycle = new ProjectLifecycleFaker().WithName("Standard").AsActiveWithStages(("Plan", "Planning"), ("Build", "Delivery"));
        _dbContext.AddProjectLifecycle(lifecycle);

        // Act
        var result = await Run(
            Row("APOLLO", ProjectStatus.Proposed, start: null) with { ProjectLifecycleId = lifecycle.Id });

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();

        var project = _portfolio.Projects.Single();
        project.ProjectLifecycleId.Should().Be(lifecycle.Id);
        project.Stages.Select(p => p.Name).Should().Equal("Plan", "Build");
    }

    [Fact]
    public async Task CreateProjects_ApprovesAProjectOnceItsLifecycleIsAssigned()
    {
        // Arrange — approval is refused without a lifecycle, so the two have to be applied in that order
        var lifecycle = new ProjectLifecycleFaker().WithName("Standard").AsActiveWithStages(("Plan", "Planning"));
        _dbContext.AddProjectLifecycle(lifecycle);

        // Act
        var result = await Run(
            Row("APOLLO", ProjectStatus.Approved, start: null) with { ProjectLifecycleId = lifecycle.Id });

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _portfolio.Projects.Single().Status.Should().Be(ProjectStatus.Approved);
    }

    [Fact]
    public async Task CreateProjects_AttachesTheProjectToItsProgram()
    {
        // Arrange
        var program = _portfolio.CreateProgram(
            "Platform", "Platform program", new LocalDateRange(_start, _end), null, null,
            EventActor.System, _dateTimeProvider.Now).Value;
        program.Activate(PpmActor.System, ProgramAncestryRoles.None, _dateTimeProvider.Now);

        // Act
        var result = await Run(Row("APOLLO", ProjectStatus.Proposed, start: null) with { ProgramId = program.Id });

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _portfolio.Projects.Single().ProgramId.Should().Be(program.Id);
    }

    [Fact]
    public async Task CreateProjects_RanksProjectsSequentiallyWithinTheirPortfolio()
    {
        // Arrange & Act — each project is ranked at the bottom, so the running max has to advance across
        // the file; otherwise every imported project would share a rank
        var result = await Run(
            Row("APOLLO", ProjectStatus.Proposed, start: null),
            Row("GEMINI", ProjectStatus.Proposed, start: null),
            Row("MERCURY", ProjectStatus.Proposed, start: null));

        // Assert
        result.Value.Rows.Should().AllSatisfy(r => r.Failed.Should().BeFalse());
        _portfolio.Projects.Select(p => p.Rank).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task CreateProjects_AssignsRolesResolvedByEmployeeNumber()
    {
        // Arrange
        var manager = new EmployeeFaker().WithEmployeeNumber("E100").Generate();
        var member = new EmployeeFaker().WithEmployeeNumber("E200").Generate();
        _dbContext.AddEmployees([manager, member]);

        // Act
        var result = await Run(Row("APOLLO", ProjectStatus.Proposed, start: null) with
        {
            ManagerEmployeeNumbers = ["E100"],
            MemberEmployeeNumbers = ["E200"],
        });

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();

        var roles = _portfolio.Projects.Single().Roles;
        roles.Should().ContainSingle(r => r.Role == ProjectRole.Manager && r.EmployeeId == manager.Id);
        roles.Should().ContainSingle(r => r.Role == ProjectRole.Member && r.EmployeeId == member.Id);
    }

    [Fact]
    public async Task CreateProjects_RejectsARowWhoseKeyIsAlreadyTaken()
    {
        // Arrange — project keys are unique in the database, so the clash is caught before the save
        _dbContext.AddProject(new ProjectFaker().WithKey(new ProjectKey("APOLLO")).Generate());

        // Act
        var result = await Run(Row("APOLLO", ProjectStatus.Proposed, start: null));

        // Assert — named per row, so the person knows which line to fix
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("APOLLO");
        _portfolio.Projects.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateProjects_RejectsTheSecondRowRepeatingAKeyWithinTheFile()
    {
        // Arrange & Act — the submission command rejects a repeated key before the run starts, but the
        // pass holds the rule too: rows are applied before anything is saved, so a repeat that reached
        // here would only surface at the unique index
        var result = await Run(
            Row("APOLLO", ProjectStatus.Proposed, start: null),
            Row("APOLLO", ProjectStatus.Proposed, start: null));

        // Assert
        result.Value.Rows.Single(r => r.ImportId == "r1").Failed.Should().BeFalse();
        result.Value.Rows.Single(r => r.ImportId == "r2").Failed.Should().BeTrue();
        _portfolio.Projects.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateProjects_RejectsARowNamingAPortfolioThatDoesNotExist()
    {
        // Arrange
        var missing = Guid.CreateVersion7();

        // Act
        var result = await Run(Row("APOLLO", ProjectStatus.Proposed, start: null) with { PortfolioId = missing });

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain(missing.ToString());
    }

    [Fact]
    public async Task CreateProjects_RejectsARowNamingAProgramInAnotherPortfolio()
    {
        // Arrange — programs are scoped to their portfolio, so one from elsewhere is as wrong as one that
        // does not exist
        var elsewhere = ProjectPortfolio.Create(
            "Elsewhere", "Another portfolio", null, EventActor.System, _dateTimeProvider.Now);
        elsewhere.Activate(PpmActor.System, _start, _dateTimeProvider.Now);
        var program = elsewhere.CreateProgram(
            "Platform", "Platform program", new LocalDateRange(_start, _end), null, null,
            EventActor.System, _dateTimeProvider.Now).Value;
        _dbContext.AddPortfolio(elsewhere);

        // Act
        var result = await Run(Row("APOLLO", ProjectStatus.Proposed, start: null) with { ProgramId = program.Id });

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain(program.Id.ToString());
    }

    [Fact]
    public async Task CreateProjects_RejectsARowNamingAnExpenditureCategoryThatDoesNotExist()
    {
        // Arrange & Act
        var result = await Run(
            Row("APOLLO", ProjectStatus.Proposed, start: null) with { ExpenditureCategoryId = _categoryId + 999 });

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("expenditure category");
    }

    [Fact]
    public async Task CreateProjects_DatesTheOpeningHistoryEntryWithCreatedOn()
    {
        // Arrange & Act — the import used to stamp every entry with the moment the file ran, so a
        // project proposed in 2024 read as though its whole life happened on upload day
        var result = await Run(Row("APOLLO", ProjectStatus.Proposed, start: null));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();

        var opening = _portfolio.Projects.Single().StatusHistory.Single();
        opening.FromStatus.Should().BeNull();
        opening.ToStatus.Should().Be(ProjectStatus.Proposed);
        opening.ChangedOn.Should().Be(At(_created));
    }

    [Fact]
    public async Task CreateProjects_DatesTheActivationWithActivatedOn()
    {
        // Arrange & Act
        var result = await Run(Row("APOLLO", ProjectStatus.Active, _start, _end));

        // Assert — two entries, each on its own date, rather than both on the run's
        result.Value.Rows.Single().Failed.Should().BeFalse();

        var history = _portfolio.Projects.Single().StatusHistory.OrderBy(h => h.Sequence).ToList();
        history.Should().HaveCount(2);
        history[0].ChangedOn.Should().Be(At(_created));
        history[1].ToStatus.Should().Be(ProjectStatus.Active);
        history[1].ChangedOn.Should().Be(At(_activated));
    }

    [Fact]
    public async Task CreateProjects_DatesTheCompletionWithClosedOn()
    {
        // Arrange & Act — reaching Completed replays Active on the way, so all three dates are in play
        var result = await Run(Row("APOLLO", ProjectStatus.Completed, _start, _end));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();

        var history = _portfolio.Projects.Single().StatusHistory.OrderBy(h => h.Sequence).ToList();
        history.Select(h => h.ToStatus).Should()
            .Equal(ProjectStatus.Proposed, ProjectStatus.Active, ProjectStatus.Completed);
        history.Select(h => h.ChangedOn).Should().Equal(At(_created), At(_activated), At(_closed));
    }

    [Fact]
    public async Task CreateProjects_DatesTheApprovalWithCreatedOn()
    {
        // Arrange — approval needs a lifecycle
        var lifecycle = new ProjectLifecycleFaker().WithName("Standard").AsActiveWithStages(("Plan", "Planning"));
        _dbContext.AddProjectLifecycle(lifecycle);

        // Act
        var result = await Run(
            Row("APOLLO", ProjectStatus.Approved, start: null) with { ProjectLifecycleId = lifecycle.Id });

        // Assert — the row carries no approval date of its own, so an approved project is still dated
        // by when it was proposed rather than by when the file happened to run
        result.Value.Rows.Single().Failed.Should().BeFalse();

        var history = _portfolio.Projects.Single().StatusHistory.OrderBy(h => h.Sequence).ToList();
        history.Select(h => h.ToStatus).Should().Equal(ProjectStatus.Proposed, ProjectStatus.Approved);
        history.Should().AllSatisfy(h => h.ChangedOn.Should().Be(At(_created)));
    }

    [Fact]
    public async Task CreateProjects_CancelsStraightFromProposedWhenTheRowNamesNoActivation()
    {
        // Arrange & Act — a project canceled before it ever started never activated, and inventing an
        // activation would put a stretch of delivery into the history that never happened
        var result = await Run(Row("APOLLO", ProjectStatus.Canceled, start: null) with { ActivatedOn = null });

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();

        var history = _portfolio.Projects.Single().StatusHistory.OrderBy(h => h.Sequence).ToList();
        history.Select(h => h.ToStatus).Should().Equal(ProjectStatus.Proposed, ProjectStatus.Canceled);
        history[1].ChangedOn.Should().Be(At(_closed));
    }

    [Fact]
    public async Task CreateProjects_ActivatesBeforeCancelingWhenTheRowNamesAnActivation()
    {
        // Arrange & Act — the other half of the same rule: a project canceled mid-flight did run for a
        // while, and the history has to show it
        var result = await Run(Row("APOLLO", ProjectStatus.Canceled, _start, _end) with { ActivatedOn = _activated });

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();

        var history = _portfolio.Projects.Single().StatusHistory.OrderBy(h => h.Sequence).ToList();
        history.Select(h => h.ToStatus).Should()
            .Equal(ProjectStatus.Proposed, ProjectStatus.Active, ProjectStatus.Canceled);
        history.Select(h => h.ChangedOn).Should().Equal(At(_created), At(_activated), At(_closed));
    }

    private static Instant At(LocalDate date) => date.AtStartOfDayInZone(DateTimeZone.Utc).ToInstant();

    private ImportProjectDto Row(string key, ProjectStatus status, LocalDate? start, LocalDate? end = null) =>
        new(
            $"Project {key}",
            $"{key} description",
            new ProjectKey(key),
            status,
            _portfolio.Id,
            null,
            _categoryId,
            null,
            null,
            null,
            start,
            end,
            _created,
            // The dates the validator would require for this status. A row builds them itself so a case
            // that is not about dates does not have to state them.
            status is ProjectStatus.Active or ProjectStatus.Completed ? _activated : null,
            status is ProjectStatus.Completed or ProjectStatus.Canceled ? _closed : null,
            [],
            [],
            [],
            [],
            []);
}
