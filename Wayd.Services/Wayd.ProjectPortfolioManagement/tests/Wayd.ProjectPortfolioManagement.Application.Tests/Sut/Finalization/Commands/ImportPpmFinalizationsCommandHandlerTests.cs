using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using NodaTime.Testing;
using NodaTime.Extensions;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Application.Finalization.Commands;
using Wayd.ProjectPortfolioManagement.Application.Finalization.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.Tests.Shared;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Finalization.Commands;

public class ImportPpmFinalizationsCommandHandlerTests : IDisposable
{
    private const string PortfolioName = "Growth";

    private static readonly LocalDate _start = new(2024, 7, 1);
    private static readonly LocalDate _end = new(2025, 6, 30);

    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly ImportPpmFinalizationsCommandHandler _handler;
    private readonly Mock<ILogger<ImportPpmFinalizationsCommandHandler>> _mockLogger;
    private readonly TestingDateTimeProvider _dateTimeProvider;

    private readonly ProjectPortfolio _portfolio;

    public ImportPpmFinalizationsCommandHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _mockLogger = new Mock<ILogger<ImportPpmFinalizationsCommandHandler>>();
        _dateTimeProvider = new TestingDateTimeProvider(new FakeClock(DateTime.UtcNow.ToInstant()));

        _handler = new ImportPpmFinalizationsCommandHandler(_dbContext, _mockLogger.Object);

        _portfolio = ProjectPortfolio.Create(PortfolioName, "Growth portfolio");
        _portfolio.Activate(_start);
        _dbContext.AddPortfolio(_portfolio);
    }

    [Fact]
    public async Task Handle_CompletesProgram_WhenAllItsProjectsAreClosed()
    {
        // Arrange
        var program = CreateActiveProgram("Platform");
        CreateProject("APOLLO", program.Id, ProjectStatus.Completed);

        var command = new ImportPpmFinalizationsCommand([ProgramRow("Platform", FinalizePpmItemStatus.Completed)]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        program.Status.Should().Be(ProgramStatus.Completed);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Fails_WhenAProgramStillHasOpenProjects()
    {
        // Arrange
        // This guard is exactly why finalization is a separate pass rather than part of the program import.
        var program = CreateActiveProgram("Platform");
        CreateProject("APOLLO", program.Id, ProjectStatus.Active);

        var command = new ImportPpmFinalizationsCommand([ProgramRow("Platform", FinalizePpmItemStatus.Completed)]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Platform");
        program.Status.Should().Be(ProgramStatus.Active);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ClosesPortfolio_WithTheRowsOwnEndDate()
    {
        // Arrange
        CreateProject("APOLLO", programId: null, ProjectStatus.Completed);

        var command = new ImportPpmFinalizationsCommand([PortfolioRow(FinalizePpmItemStatus.Closed, _end)]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _portfolio.Status.Should().Be(ProjectPortfolioStatus.Closed);
        _portfolio.DateRange!.End.Should().Be(_end);
    }

    [Fact]
    public async Task Handle_ClosesThenArchivesPortfolio_WhenStatusIsArchived()
    {
        // Arrange
        // Archiving is only legal from Closed, so both transitions have to run in order.
        CreateProject("APOLLO", programId: null, ProjectStatus.Completed);

        var command = new ImportPpmFinalizationsCommand([PortfolioRow(FinalizePpmItemStatus.Archived, _end)]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _portfolio.Status.Should().Be(ProjectPortfolioStatus.Archived);
        _portfolio.DateRange!.End.Should().Be(_end);
    }

    [Fact]
    public async Task Handle_AppliesProgramsBeforePortfolios_RegardlessOfRowOrder()
    {
        // Arrange
        // A portfolio cannot close while one of its programs is open, so the file's order must not decide
        // the outcome — the portfolio row is deliberately listed first here.
        var program = CreateActiveProgram("Platform");
        CreateProject("APOLLO", program.Id, ProjectStatus.Completed);

        var command = new ImportPpmFinalizationsCommand([
            PortfolioRow(FinalizePpmItemStatus.Closed, _end),
            ProgramRow("Platform", FinalizePpmItemStatus.Completed),
        ]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        program.Status.Should().Be(ProgramStatus.Completed);
        _portfolio.Status.Should().Be(ProjectPortfolioStatus.Closed);
    }

    [Fact]
    public async Task Handle_CancelsProgram_WhenStatusIsCancelled()
    {
        // Arrange
        var program = CreateActiveProgram("Platform");
        CreateProject("APOLLO", program.Id, ProjectStatus.Cancelled);

        var command = new ImportPpmFinalizationsCommand([ProgramRow("Platform", FinalizePpmItemStatus.Cancelled)]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        program.Status.Should().Be(ProgramStatus.Cancelled);
    }

    [Fact]
    public async Task Handle_Fails_WhenTheProgramCannotBeResolvedInItsPortfolio()
    {
        // Arrange
        var command = new ImportPpmFinalizationsCommand([ProgramRow("Nonexistent", FinalizePpmItemStatus.Completed)]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Nonexistent");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Fails_WhenThePortfolioCannotBeResolved()
    {
        // Arrange
        var row = PortfolioRow(FinalizePpmItemStatus.Closed, _end) with { Name = "Nonexistent" };

        // Act
        var result = await _handler.Handle(new ImportPpmFinalizationsCommand([row]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Nonexistent");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    private Program CreateActiveProgram(string name)
    {
        var program = _portfolio.CreateProgram(name, $"{name} program", new LocalDateRange(_start, _end), null, null, _dateTimeProvider.Now).Value;
        program.Activate();

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
            _dateTimeProvider.Now).Value;

        switch (status)
        {
            case ProjectStatus.Completed:
                project.Activate();
                project.Complete();
                break;
            case ProjectStatus.Cancelled:
                project.Cancel();
                break;
            case ProjectStatus.Active:
                project.Activate();
                break;
        }
    }

    private static FinalizePpmItemDto ProgramRow(string name, FinalizePpmItemStatus status) =>
        new(FinalizePpmItemType.Program, name, PortfolioName, status, null);

    private static FinalizePpmItemDto PortfolioRow(FinalizePpmItemStatus status, LocalDate endDate) =>
        new(FinalizePpmItemType.Portfolio, PortfolioName, null, status, endDate);

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
