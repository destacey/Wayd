using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Scoring.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Portfolios.Scoring.Commands;

public class ClearPortfolioScoringModelCommandHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly ClearPortfolioScoringModelCommandHandler _handler;
    private readonly Mock<ILogger<ClearPortfolioScoringModelCommandHandler>> _mockLogger = new();
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new NodaTime.Testing.FakeClock(Instant.FromUtc(2026, 5, 1, 0, 0)));
    private readonly ProjectPortfolioFaker _portfolioFaker = new();

    public ClearPortfolioScoringModelCommandHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _handler = new ClearPortfolioScoringModelCommandHandler(_dbContext, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WhenAssigned_ClearsModelAndSaves()
    {
        // Arrange
        var portfolio = _portfolioFaker.AsActive(_dateTimeProvider);
        portfolio.SetPrivate(p => p.ScoringModelId, (Guid?)Guid.NewGuid());
        _dbContext.AddPortfolio(portfolio);

        var command = new ClearPortfolioScoringModelCommand(portfolio.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        portfolio.ScoringModelId.Should().BeNull();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenPortfolioNotFound_ReturnsFailure()
    {
        // Arrange
        var command = new ClearPortfolioScoringModelCommand(Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Portfolio not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => _dbContext.Dispose();
}
