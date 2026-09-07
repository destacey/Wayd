using FluentAssertions;
using NodaTime;
using NodaTime.Testing;
using NodaTime.Extensions;
using Wayd.Common.Application.Imports;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Imports;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Application.Finalization.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Finalization.Imports;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.Tests.Shared;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Finalization.Imports;

public sealed class PpmFinalizationImportDefinitionTests : IDisposable
{
    private const int FinalizePass = 0;

    private static readonly LocalDate _start = new(2024, 7, 1);
    private static readonly LocalDate _end = new(2025, 6, 30);

    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly PpmFinalizationImportDefinition _definition;

    private readonly ProjectPortfolio _portfolio;

    public PpmFinalizationImportDefinitionTests()
    {
        _dateTimeProvider = new TestingDateTimeProvider(new FakeClock(DateTime.UtcNow.ToInstant()));
        _definition = new PpmFinalizationImportDefinition(_dbContext, new ImportPayloadSerializer());

        _portfolio = ProjectPortfolio.Create("Growth", "Growth portfolio");
        _portfolio.Activate(PpmActor.System, _start);
        _dbContext.AddPortfolio(_portfolio);
    }

    public void Dispose() => _dbContext.Dispose();

    private ImportProcessRow[] Rows(params FinalizePpmItemDto[] items) =>
        [.. items.Select((i, n) => ImportProcessRow.Create($"r{n + 1}", n + 1, _definition.SerializeRow(i)))];

    private Task<CSharpFunctionalExtensions.Result<ImportPassResult>> Run(params FinalizePpmItemDto[] items) =>
        _definition.ExecutePass(
            Guid.CreateVersion7(), FinalizePass, Rows(items), isFinalChunk: true, TestContext.Current.CancellationToken);

    [Fact]
    public void Definition_IsAtomicAndCannotBeChunked()
    {
        // Arrange & Act & Assert — program rows are applied before portfolio rows whatever order the file
        // lists them in, so the pass needs the whole set
        _definition.Atomicity.Should().Be(ImportAtomicity.Atomic);
        _definition.Passes.Single().Scope.Should().Be(ImportPassScope.WholeSet);
    }

    [Fact]
    public async Task Finalize_CompletesAProgramOnceAllItsProjectsAreClosed()
    {
        // Arrange
        var program = CreateActiveProgram("Platform");
        CreateProject("APOLLO", program.Id, ProjectStatus.Completed);

        // Act
        var result = await Run(ProgramRow(program.Id, FinalizePpmItemStatus.Completed));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeFalse();
        program.Status.Should().Be(ProgramStatus.Completed);
        outcome.CreatedEntityId.Should().Be(program.Id);
    }

    [Fact]
    public async Task Finalize_RejectsAProgramThatStillHasOpenProjects()
    {
        // Arrange — this guard is exactly why finalization is a separate import rather than part of the
        // program import
        var program = CreateActiveProgram("Platform");
        CreateProject("APOLLO", program.Id, ProjectStatus.Active);

        // Act
        var result = await Run(ProgramRow(program.Id, FinalizePpmItemStatus.Completed));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("Platform");
        program.Status.Should().Be(ProgramStatus.Active);
    }

    [Fact]
    public async Task Finalize_ClosesAPortfolioWithTheRowsOwnEndDate()
    {
        // Arrange
        CreateProject("APOLLO", programId: null, ProjectStatus.Completed);

        // Act
        var result = await Run(PortfolioRow(FinalizePpmItemStatus.Closed, _end));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _portfolio.Status.Should().Be(ProjectPortfolioStatus.Closed);
        _portfolio.DateRange!.End.Should().Be(_end);
    }

    [Fact]
    public async Task Finalize_ClosesThenArchivesAPortfolioAskedToArchive()
    {
        // Arrange — archiving is only legal from Closed, so both transitions have to run in order
        CreateProject("APOLLO", programId: null, ProjectStatus.Completed);

        // Act
        var result = await Run(PortfolioRow(FinalizePpmItemStatus.Archived, _end));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _portfolio.Status.Should().Be(ProjectPortfolioStatus.Archived);
        _portfolio.DateRange!.End.Should().Be(_end);
    }

    [Fact]
    public async Task Finalize_AppliesProgramsBeforePortfoliosRegardlessOfRowOrder()
    {
        // Arrange — a portfolio cannot close while one of its programs is open, so the file's order must
        // not decide the outcome; the portfolio row is deliberately listed first
        var program = CreateActiveProgram("Platform");
        CreateProject("APOLLO", program.Id, ProjectStatus.Completed);

        // Act
        var result = await Run(
            PortfolioRow(FinalizePpmItemStatus.Closed, _end),
            ProgramRow(program.Id, FinalizePpmItemStatus.Completed));

        // Assert
        result.Value.Rows.Should().AllSatisfy(r => r.Failed.Should().BeFalse());
        program.Status.Should().Be(ProgramStatus.Completed);
        _portfolio.Status.Should().Be(ProjectPortfolioStatus.Closed);
    }

    [Fact]
    public async Task Finalize_CancelsAProgramAskedToCancel()
    {
        // Arrange
        var program = CreateActiveProgram("Platform");
        CreateProject("APOLLO", program.Id, ProjectStatus.Canceled);

        // Act
        var result = await Run(ProgramRow(program.Id, FinalizePpmItemStatus.Canceled));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        program.Status.Should().Be(ProgramStatus.Canceled);
    }

    [Fact]
    public async Task Finalize_RejectsARowNamingAProgramThatDoesNotExist()
    {
        // Arrange
        var missing = Guid.CreateVersion7();

        // Act
        var result = await Run(ProgramRow(missing, FinalizePpmItemStatus.Completed));

        // Assert — named per row, so the person knows which line to fix
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain(missing.ToString());
    }

    [Fact]
    public async Task Finalize_RejectsARowNamingAPortfolioThatDoesNotExist()
    {
        // Arrange
        var missing = Guid.CreateVersion7();

        // Act
        var result = await Run(PortfolioRow(FinalizePpmItemStatus.Closed, _end) with { Id = missing });

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain(missing.ToString());
    }

    private Program CreateActiveProgram(string name)
    {
        var program = _portfolio.CreateProgram(
            name, $"{name} program", new LocalDateRange(_start, _end), null, null,
            EventActor.System, _dateTimeProvider.Now).Value;
        program.Activate(PpmActor.System, ProgramAncestryRoles.None);

        return program;
    }

    private void CreateProject(string key, Guid? programId, ProjectStatus status)
    {
        var project = _portfolio.CreateProject(
            $"Project {key}",
            $"{key} description",
            new ProjectKey(key),
            1,
            new LocalDateRange(_start, _end),
            programId,
            null,
            null,
            null,
            null,
            _dateTimeProvider.Now, PpmActor.System).Value;

        switch (status)
        {
            case ProjectStatus.Completed:
                project.Activate(PpmActor.System, ProjectAncestryRoles.None, _dateTimeProvider.Now);
                project.Complete(PpmActor.System, ProjectAncestryRoles.None, _dateTimeProvider.Now);
                break;
            case ProjectStatus.Canceled:
                project.Cancel(PpmActor.System, ProjectAncestryRoles.None, _dateTimeProvider.Now);
                break;
            case ProjectStatus.Active:
                project.Activate(PpmActor.System, ProjectAncestryRoles.None, _dateTimeProvider.Now);
                break;
        }
    }

    private static FinalizePpmItemDto ProgramRow(Guid programId, FinalizePpmItemStatus status) =>
        new(FinalizePpmItemType.Program, programId, status, null);

    private FinalizePpmItemDto PortfolioRow(FinalizePpmItemStatus status, LocalDate endDate) =>
        new(FinalizePpmItemType.Portfolio, _portfolio.Id, status, endDate);
}
