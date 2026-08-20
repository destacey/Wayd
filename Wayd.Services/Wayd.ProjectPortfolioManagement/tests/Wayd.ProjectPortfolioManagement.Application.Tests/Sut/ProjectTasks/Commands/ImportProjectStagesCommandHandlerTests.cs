using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using NodaTime.Testing;
using NodaTime.Extensions;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Commands;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;

using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.ProjectTasks.Commands;

public class ImportProjectStagesCommandHandlerTests : IDisposable
{
    private const string ProjectKeyValue = "APOLLO";
    private const string StageName = "Build";

    private static readonly LocalDate _start = new(2024, 7, 1);
    private static readonly LocalDate _end = new(2025, 6, 30);

    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly ImportProjectStagesCommandHandler _handler;
    private readonly Mock<ILogger<ImportProjectStagesCommandHandler>> _mockLogger;
    private readonly TestingDateTimeProvider _dateTimeProvider;

    private readonly Project _project;

    public ImportProjectStagesCommandHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _mockLogger = new Mock<ILogger<ImportProjectStagesCommandHandler>>();
        _dateTimeProvider = new TestingDateTimeProvider(new FakeClock(DateTime.UtcNow.ToInstant()));

        _handler = new ImportProjectStagesCommandHandler(_dbContext, _mockLogger.Object);

        // A project with an assigned lifecycle, which is where its stages come from.
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
            _dateTimeProvider.Now, PpmActor.System).Value;

        var lifecycle = new ProjectLifecycleFaker().WithName("Standard").AsActiveWithStages((StageName, "Delivery"), ("Close", "Closure"));
        _project.AssignLifecycle(PpmActor.System, ProjectAncestryRoles.None, lifecycle);

        _dbContext.AddProject(_project);
    }

    [Fact]
    public async Task Handle_SetsTheStageStatusExactlyAsGiven()
    {
        // Arrange — the status is applied verbatim, not derived from any tasks.
        var command = new ImportProjectStagesCommand([Row(StageName, TaskStatus.Completed)]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _project.Stages.Single(p => p.Name == StageName).Status.Should().Be(TaskStatus.Completed);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_SetsEachStageIndependently()
    {
        // Arrange
        var command = new ImportProjectStagesCommand([
            Row("Build", TaskStatus.Completed),
            Row("Close", TaskStatus.InProgress),
        ]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _project.Stages.Single(p => p.Name == "Build").Status.Should().Be(TaskStatus.Completed);
        _project.Stages.Single(p => p.Name == "Close").Status.Should().Be(TaskStatus.InProgress);
    }

    [Fact]
    public async Task Handle_Fails_WhenTheProjectCannotBeResolved()
    {
        // Arrange
        var row = Row(StageName, TaskStatus.Completed) with { ProjectKey = new ProjectKey("GEMINI") };

        // Act
        var result = await _handler.Handle(new ImportProjectStagesCommand([row]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("GEMINI");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Fails_WhenTheStageIsNotOnTheProjectsLifecycle()
    {
        // Arrange
        var row = Row("Nonexistent", TaskStatus.Completed);

        // Act
        var result = await _handler.Handle(new ImportProjectStagesCommand([row]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Nonexistent");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    private static ImportProjectStageDto Row(string stageName, TaskStatus status) =>
        new(new ProjectKey(ProjectKeyValue), stageName, status);

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
