using FluentAssertions;
using Microsoft.Extensions.Logging;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;
using Moq;
using NodaTime.Extensions;
using NodaTime.Testing;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.StrategicInitiatives.Commands;

public class ManageStrategicInitiativeProjectsCommandHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly ManageStrategicInitiativeProjectsCommandHandler _handler;
    private readonly Mock<ILogger<ManageStrategicInitiativeProjectsCommandHandler>> _mockLogger;
    private readonly TestingDateTimeProvider _dateTimeProvider;

    private readonly StrategicInitiativeFaker _initiativeFaker;
    private readonly ProjectFaker _projectFaker;

    public ManageStrategicInitiativeProjectsCommandHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _mockLogger = new Mock<ILogger<ManageStrategicInitiativeProjectsCommandHandler>>();
        _dateTimeProvider = new TestingDateTimeProvider(new FakeClock(DateTime.UtcNow.ToInstant()));

        _handler = new ManageStrategicInitiativeProjectsCommandHandler(_dbContext, _mockLogger.Object);

        _initiativeFaker = new StrategicInitiativeFaker(_dateTimeProvider);
        _projectFaker = new ProjectFaker();
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenStrategicInitiativeNotFound()
    {
        // Arrange
        var command = new ManageStrategicInitiativeProjectsCommand(Guid.NewGuid(), []);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Strategic Initiative not found.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenAnyProjectDoesNotExist()
    {
        // Arrange
        var portfolioId = Guid.NewGuid();
        var initiative = _initiativeFaker.AsActive(_dateTimeProvider, portfolioId);
        _dbContext.AddStrategicInitiative(initiative);

        var existingProject = _projectFaker.AsActive(_dateTimeProvider, portfolioId);
        _dbContext.AddProject(existingProject);

        var command = new ManageStrategicInitiativeProjectsCommand(
            initiative.Id,
            [existingProject.Id, Guid.NewGuid()]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("One or more projects do not exist.");
        initiative.StrategicInitiativeProjects.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldLinkProjects_WhenAllProjectsExistInSamePortfolio()
    {
        // Arrange
        var portfolioId = Guid.NewGuid();
        var initiative = _initiativeFaker.AsActive(_dateTimeProvider, portfolioId);
        _dbContext.AddStrategicInitiative(initiative);

        var projectA = _projectFaker.AsActive(_dateTimeProvider, portfolioId);
        var projectB = _projectFaker.AsActive(_dateTimeProvider, portfolioId);
        _dbContext.AddProject(projectA);
        _dbContext.AddProject(projectB);

        var command = new ManageStrategicInitiativeProjectsCommand(
            initiative.Id,
            [projectA.Id, projectB.Id]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        initiative.StrategicInitiativeProjects
            .Select(p => p.ProjectId)
            .Should()
            .BeEquivalentTo([projectA.Id, projectB.Id]);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldLinkProjects_FromOtherPortfolios()
    {
        // Arrange — initiative in portfolio A, projects across A, B, and C
        var portfolioA = Guid.NewGuid();
        var portfolioB = Guid.NewGuid();
        var portfolioC = Guid.NewGuid();

        var initiative = _initiativeFaker.AsActive(_dateTimeProvider, portfolioA);
        _dbContext.AddStrategicInitiative(initiative);

        var sameProject = _projectFaker.AsActive(_dateTimeProvider, portfolioA);
        var otherPortfolioProject1 = _projectFaker.AsActive(_dateTimeProvider, portfolioB);
        var otherPortfolioProject2 = _projectFaker.AsActive(_dateTimeProvider, portfolioC);
        _dbContext.AddProject(sameProject);
        _dbContext.AddProject(otherPortfolioProject1);
        _dbContext.AddProject(otherPortfolioProject2);

        var command = new ManageStrategicInitiativeProjectsCommand(
            initiative.Id,
            [sameProject.Id, otherPortfolioProject1.Id, otherPortfolioProject2.Id]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        initiative.StrategicInitiativeProjects
            .Select(p => p.ProjectId)
            .Should()
            .BeEquivalentTo([sameProject.Id, otherPortfolioProject1.Id, otherPortfolioProject2.Id]);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRemoveProjects_WhenProjectIdOmittedFromCommand()
    {
        // Arrange — two projects already linked; command keeps only one
        var portfolioId = Guid.NewGuid();
        var initiative = _initiativeFaker.AsActive(_dateTimeProvider, portfolioId);

        var keepProject = _projectFaker.AsActive(_dateTimeProvider, portfolioId);
        var removeProject = _projectFaker.AsActive(_dateTimeProvider, portfolioId);
        _dbContext.AddProject(keepProject);
        _dbContext.AddProject(removeProject);

        initiative.ManageProjects([keepProject.Id, removeProject.Id]);
        _dbContext.AddStrategicInitiative(initiative);

        var command = new ManageStrategicInitiativeProjectsCommand(
            initiative.Id,
            [keepProject.Id]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        initiative.StrategicInitiativeProjects
            .Select(p => p.ProjectId)
            .Should()
            .BeEquivalentTo([keepProject.Id]);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenStrategicInitiativeIsCompleted()
    {
        // Arrange
        var portfolioId = Guid.NewGuid();
        var initiative = _initiativeFaker.AsCompleted(_dateTimeProvider, portfolioId);
        _dbContext.AddStrategicInitiative(initiative);

        var project = _projectFaker.AsActive(_dateTimeProvider, portfolioId);
        _dbContext.AddProject(project);

        var command = new ManageStrategicInitiativeProjectsCommand(initiative.Id, [project.Id]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Projects cannot be added or removed for closed strategic initiatives.");
        initiative.StrategicInitiativeProjects.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
