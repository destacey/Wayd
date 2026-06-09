using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Ranking.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Portfolios.Ranking.Commands;

public class MoveProjectRanksCommandHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly MoveProjectRanksCommandHandler _handler;
    private readonly Mock<ILogger<MoveProjectRanksCommandHandler>> _mockLogger = new();
    private readonly Mock<ICurrentUser> _mockCurrentUser = new();
    private readonly Guid _employeeId = Guid.NewGuid();
    private readonly ProjectPortfolioFaker _portfolioFaker = new();
    private readonly ProjectFaker _projectFaker = new();

    public MoveProjectRanksCommandHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _mockCurrentUser.Setup(u => u.GetEmployeeId()).Returns(_employeeId);
        _handler = new MoveProjectRanksCommandHandler(_dbContext, _mockCurrentUser.Object, _mockLogger.Object);
    }

    private Project Project(string name, double rank) =>
        _projectFaker.WithName(name).WithStatus(ProjectStatus.Active).WithRank(rank).Generate();

    private ProjectPortfolio Portfolio(params Project[] projects) =>
        _portfolioFaker.WithStatus(ProjectPortfolioStatus.Active).WithRoles(new Dictionary<ProjectPortfolioRole, HashSet<Guid>>
        {
            [ProjectPortfolioRole.Owner] = [_employeeId],
        }).Generate().WithProjects(projects);

    [Fact]
    public async Task Handle_WhenValid_MovesAndSaves()
    {
        // Arrange
        var after = Project("After", 1000d);
        var before = Project("Before", 2000d);
        var moved = Project("Moved", 90000d);
        var portfolio = Portfolio(after, before, moved);
        _dbContext.AddPortfolio(portfolio);

        var command = new MoveProjectRanksCommand(portfolio.Id, [moved.Id], after.Id, before.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        moved.Rank.Should().BeGreaterThan(1000d).And.BeLessThan(2000d);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenEmployeeIdNull_ReturnsFailureWithoutSaving()
    {
        // Arrange
        _mockCurrentUser.Setup(u => u.GetEmployeeId()).Returns((Guid?)null);
        var after = Project("After", 1000d);
        var moved = Project("Moved", 90000d);
        var portfolio = Portfolio(after, moved);
        _dbContext.AddPortfolio(portfolio);

        var command = new MoveProjectRanksCommand(portfolio.Id, [moved.Id], after.Id, null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenPortfolioNotFound_ReturnsFailureWithoutSaving()
    {
        // Arrange
        var command = new MoveProjectRanksCommand(Guid.NewGuid(), [Guid.NewGuid()], Guid.NewGuid(), null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Portfolio not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenActorNotAuthorized_ReturnsFailureWithoutSaving()
    {
        // Arrange — actor has no portfolio role.
        _mockCurrentUser.Setup(u => u.GetEmployeeId()).Returns(Guid.NewGuid());
        var after = Project("After", 1000d);
        var moved = Project("Moved", 90000d);
        var portfolio = Portfolio(after, moved);
        _dbContext.AddPortfolio(portfolio);

        var command = new MoveProjectRanksCommand(portfolio.Id, [moved.Id], after.Id, null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not authorized");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenDomainValidationFails_BubblesFailureWithoutSaving()
    {
        // Arrange — anchor also appears in the batch → domain failure.
        var after = Project("After", 1000d);
        var moved = Project("Moved", 90000d);
        var portfolio = Portfolio(after, moved);
        _dbContext.AddPortfolio(portfolio);

        var command = new MoveProjectRanksCommand(portfolio.Id, [moved.Id, after.Id], after.Id, null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => _dbContext.Dispose();
}