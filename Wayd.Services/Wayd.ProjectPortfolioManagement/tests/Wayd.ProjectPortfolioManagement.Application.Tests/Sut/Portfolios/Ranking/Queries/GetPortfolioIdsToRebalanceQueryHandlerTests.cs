using FluentAssertions;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Ranking.Queries;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Portfolios.Ranking.Queries;

public class GetPortfolioIdsToRebalanceQueryHandlerTests
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly GetPortfolioIdsToRebalanceQueryHandler _handler;

    public GetPortfolioIdsToRebalanceQueryHandlerTests()
    {
        _handler = new GetPortfolioIdsToRebalanceQueryHandler(_dbContext);
    }

    [Fact]
    public async Task Handle_ReturnsOnlyPortfoliosWithAnAdjacentGapBelowTheThreshold()
    {
        // Arrange — ranks a thousand apart are healthy; two within a unit of each other are not
        var spaced = AddPortfolio(ProjectPortfolioStatus.Active, 1000, 2000, 3000);
        var crowded = AddPortfolio(ProjectPortfolioStatus.Active, 1000, 2000, 2000.5);

        // Act
        var ids = await _handler.Handle(new GetPortfolioIdsToRebalanceQuery(), TestContext.Current.CancellationToken);

        // Assert
        ids.Should().Equal(crowded.Id);
        ids.Should().NotContain(spaced.Id);
    }

    [Fact]
    public async Task Handle_ComparesRanksInOrder_NotInInsertionOrder()
    {
        // Arrange — inserted out of order, but 1000 and 1000.25 are still adjacent once sorted
        var crowded = AddPortfolio(ProjectPortfolioStatus.Active, 1000.25, 5000, 1000);

        // Act
        var ids = await _handler.Handle(new GetPortfolioIdsToRebalanceQuery(), TestContext.Current.CancellationToken);

        // Assert
        ids.Should().Equal(crowded.Id);
    }

    [Fact]
    public async Task Handle_SkipsAnArchivedPortfolio()
    {
        // Arrange — archived portfolios are read-only, however crowded their ranks
        AddPortfolio(ProjectPortfolioStatus.Archived, 1000, 1000.1);

        // Act
        var ids = await _handler.Handle(new GetPortfolioIdsToRebalanceQuery(), TestContext.Current.CancellationToken);

        // Assert
        ids.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_SkipsAPortfolioWithFewerThanTwoProjects()
    {
        // Arrange — a single rank has no gap to measure
        AddPortfolio(ProjectPortfolioStatus.Active, 1000);
        AddPortfolio(ProjectPortfolioStatus.Active);

        // Act
        var ids = await _handler.Handle(new GetPortfolioIdsToRebalanceQuery(), TestContext.Current.CancellationToken);

        // Assert
        ids.Should().BeEmpty();
    }

    private ProjectPortfolio AddPortfolio(ProjectPortfolioStatus status, params double[] ranks)
    {
        var portfolio = new ProjectPortfolioFaker().WithStatus(status).Generate();
        _dbContext.AddPortfolio(portfolio);
        _dbContext.AddProjects(ranks.Select(rank => new ProjectFaker().WithPortfolioId(portfolio.Id).WithRank(rank).Generate()));
        return portfolio;
    }
}
