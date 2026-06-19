using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Domain.Scoring;
using Wayd.Common.Domain.Tests.Data;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Scoring.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Portfolios.Scoring.Commands;

public class AssignPortfolioScoringModelCommandHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly AssignPortfolioScoringModelCommandHandler _handler;
    private readonly Mock<ILogger<AssignPortfolioScoringModelCommandHandler>> _mockLogger = new();
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new NodaTime.Testing.FakeClock(Instant.FromUtc(2026, 5, 1, 0, 0)));
    private readonly ProjectPortfolioFaker _portfolioFaker = new();
    private readonly ScoringModelFaker _scoringModelFaker = new();

    public AssignPortfolioScoringModelCommandHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _handler = new AssignPortfolioScoringModelCommandHandler(_dbContext, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WhenValid_AssignsModelAndSaves()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsActive(_dateTimeProvider);
        var model = _scoringModelFaker.AsActiveWsjf();
        _dbContext.AddPortfolio(portfolio);
        _dbContext.AddScoringModel(model);

        var command = new AssignPortfolioScoringModelCommand(portfolio.Id, model.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        portfolio.ScoringModelId.Should().Be(model.Id);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenPortfolioNotFound_ReturnsFailure()
    {
        // Arrange
        var model = _scoringModelFaker.AsActiveWsjf();
        _dbContext.AddScoringModel(model);

        var command = new AssignPortfolioScoringModelCommand(Guid.NewGuid(), model.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Portfolio not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenModelNotFound_ReturnsFailure()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsActive(_dateTimeProvider);
        _dbContext.AddPortfolio(portfolio);

        var command = new AssignPortfolioScoringModelCommand(portfolio.Id, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Scoring Model not found");
        portfolio.ScoringModelId.Should().BeNull();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenModelNotActive_ReturnsFailure()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsActive(_dateTimeProvider);
        var proposedModel = ScoringModel.Create("Proposed", "Not active yet.");
        _dbContext.AddPortfolio(portfolio);
        _dbContext.AddScoringModel(proposedModel);

        var command = new AssignPortfolioScoringModelCommand(portfolio.Id, proposedModel.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("active");
        portfolio.ScoringModelId.Should().BeNull();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => _dbContext.Dispose();
}
