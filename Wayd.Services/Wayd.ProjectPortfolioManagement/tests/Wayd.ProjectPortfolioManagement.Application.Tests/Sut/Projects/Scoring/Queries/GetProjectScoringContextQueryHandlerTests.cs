using FluentAssertions;
using Wayd.ProjectPortfolioManagement.Application.Projects.Scoring.Queries;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Scoring.Queries;

public class GetProjectScoringContextQueryHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly GetProjectScoringContextQueryHandler _handler;
    private readonly ProjectFaker _projectFaker = new();
    private readonly ProjectPortfolioFaker _portfolioFaker = new();

    public GetProjectScoringContextQueryHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _handler = new GetProjectScoringContextQueryHandler(_dbContext);
    }

    [Fact]
    public async Task Handle_WhenProjectNotFound_ReturnsNull()
    {
        // Arrange & Act
        var result = await _handler.Handle(
            new GetProjectScoringContextQuery(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        // Assert — a missing project must be distinguishable from a project without a model (404 vs 200).
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenProjectExistsButPortfolioHasNoModel_ReturnsEmptyContext()
    {
        // Arrange
        var portfolio = _portfolioFaker.WithData(id: Guid.NewGuid()).Generate();
        var project = _projectFaker.WithData(portfolioId: portfolio.Id).Generate();
        project.SetPrivate(p => p.Portfolio, portfolio);
        _dbContext.AddProject(project);

        // Act
        var result = await _handler.Handle(
            new GetProjectScoringContextQuery(project.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.ScoringModel.Should().BeNull();
        result.CurrentScore.Should().BeNull();
        result.ScoringModelArchived.Should().BeFalse();
    }

    public void Dispose() => _dbContext.Dispose();
}
