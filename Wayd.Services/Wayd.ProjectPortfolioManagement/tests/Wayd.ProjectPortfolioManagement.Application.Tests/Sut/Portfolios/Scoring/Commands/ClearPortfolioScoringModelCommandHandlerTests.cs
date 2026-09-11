using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Tests.Data;
using Wayd.ProjectPortfolioManagement.Application.Common;
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
    private readonly Mock<ICurrentPrincipal> _mockCurrentPrincipal = new();
    private readonly Mock<ICurrentUser> _mockCurrentUser = new();
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new NodaTime.Testing.FakeClock(Instant.FromUtc(2026, 5, 1, 0, 0)));
    private readonly ProjectPortfolioFaker _portfolioFaker = new();
    private readonly ScoringModelFaker _scoringModelFaker = new();

    public ClearPortfolioScoringModelCommandHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _mockCurrentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());
        _mockCurrentPrincipal
            .Setup(p => p.HasPermission(PpmAuthorizationExtensions.PpmAdministratorPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockCurrentUser.Setup(u => u.GetUserId()).Returns(Guid.NewGuid().ToString());

        _handler = new ClearPortfolioScoringModelCommandHandler(
            _dbContext, _mockCurrentPrincipal.Object, _mockCurrentUser.Object,
            _mockLogger.Object, _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_WhenAssigned_ClearsModelAndSaves()
    {
        // Arrange
        // Both halves of the assignment, as the handler's Include(p => p.ScoringModel) loads them
        var model = _scoringModelFaker.AsActiveWsjf();
        var portfolio = _portfolioFaker.AsActive(_dateTimeProvider);
        portfolio.SetPrivate(p => p.ScoringModelId, (Guid?)model.Id);
        portfolio.SetPrivate(p => p.ScoringModel, model);
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
