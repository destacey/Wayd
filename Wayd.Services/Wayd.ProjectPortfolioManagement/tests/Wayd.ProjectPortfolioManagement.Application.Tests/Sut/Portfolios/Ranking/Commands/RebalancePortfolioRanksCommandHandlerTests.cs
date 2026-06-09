using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Common.Application.Identity;
using Wayd.Common.Application.Interfaces;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Ranking.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Portfolios.Ranking.Commands;

public class RebalancePortfolioRanksCommandHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly RebalancePortfolioRanksCommandHandler _handler;
    private readonly Mock<ILogger<RebalancePortfolioRanksCommandHandler>> _mockLogger = new();
    private readonly Mock<ICurrentUser> _mockCurrentUser = new();
    private readonly Guid _employeeId = Guid.NewGuid();
    private readonly ProjectPortfolioFaker _portfolioFaker = new();
    private readonly ProjectFaker _projectFaker = new();

    public RebalancePortfolioRanksCommandHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _mockCurrentUser.Setup(u => u.GetEmployeeId()).Returns(_employeeId);
        _handler = new RebalancePortfolioRanksCommandHandler(_dbContext, _mockCurrentUser.Object, _mockLogger.Object);
    }

    private Project Project(string name, double rank) =>
        _projectFaker.WithName(name).WithStatus(ProjectStatus.Active).WithRank(rank).Generate();

    private ProjectPortfolio Portfolio(bool authorized, params Project[] projects) =>
        _portfolioFaker.WithStatus(ProjectPortfolioStatus.Active).WithRoles(new Dictionary<ProjectPortfolioRole, HashSet<Guid>>
        {
            [ProjectPortfolioRole.Owner] = [authorized ? _employeeId : Guid.NewGuid()],
        }).Generate().WithProjects(projects);

    [Fact]
    public async Task Handle_WhenValid_RebalancesAndSaves()
    {
        // Arrange
        var first = Project("First", 1000.5d);
        var second = Project("Second", 1000.75d);
        var third = Project("Third", 1001d);
        var portfolio = Portfolio(authorized: true, first, second, third);
        _dbContext.AddPortfolio(portfolio);

        var command = new RebalancePortfolioRanksCommand(portfolio.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        first.Rank.Should().Be(1000d);
        second.Rank.Should().Be(2000d);
        third.Rank.Should().Be(3000d);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenPortfolioNotFound_ReturnsFailureWithoutSaving()
    {
        // Arrange
        var command = new RebalancePortfolioRanksCommand(Guid.NewGuid());

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
        // Arrange
        var project = Project("A", 1234.5d);
        var portfolio = Portfolio(authorized: false, project);
        _dbContext.AddPortfolio(portfolio);

        var command = new RebalancePortfolioRanksCommand(portfolio.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not authorized");
        project.Rank.Should().Be(1234.5d); // unchanged
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenSystemContext_BypassesAuthorizationAndSaves()
    {
        // Arrange — the scheduled job runs as the system identity: no employee id, system user id,
        // and not a portfolio owner. The rebalance should still proceed.
        _mockCurrentUser.Setup(u => u.GetEmployeeId()).Returns((Guid?)null);
        _mockCurrentUser.Setup(u => u.GetUserId()).Returns(SystemIdentity.UserId);

        var project = Project("A", 1234.5d);
        var portfolio = Portfolio(authorized: false, project);
        _dbContext.AddPortfolio(portfolio);

        var command = new RebalancePortfolioRanksCommand(portfolio.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Rank.Should().Be(1000d);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    public void Dispose() => _dbContext.Dispose();
}