using FluentAssertions;
using Wayd.ProjectPortfolioManagement.Application.Projects.Scoring.Queries;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Scoring.Queries;

public class GetProjectScoreQueryHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly GetProjectScoreQueryHandler _handler;
    private readonly ProjectScoreFaker _scoreFaker = new();
    private readonly ProjectScoreRatingFaker _ratingFaker = new();
    private readonly ProjectScoreOutputFaker _outputFaker = new();

    public GetProjectScoreQueryHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _handler = new GetProjectScoreQueryHandler(_dbContext);
    }

    [Fact]
    public async Task Handle_ReturnsFrozenSnapshotWithRatingsAndOutputs()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var scoreId = Guid.NewGuid();
        var rating = _ratingFaker
            .WithProjectScoreId(scoreId).WithCriterionToken("BV").WithRatingValue(8m).WithOrder(1)
            .Generate();
        var output = _outputFaker
            .WithProjectScoreId(scoreId).WithToken("Score").WithValue(4m).WithIsPrimary(true).WithOrder(1)
            .Generate();
        var score = _scoreFaker
            .WithId(scoreId).WithProjectId(projectId).WithPrimaryValue(4m).WithRatings([rating]).WithOutputs([output])
            .Generate();
        _dbContext.AddProjectScore(score);

        // Act
        var result = await _handler.Handle(
            new GetProjectScoreQuery(projectId, scoreId),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(scoreId);
        result.PrimaryValue.Should().Be(4m);
        result.Ratings.Should().ContainSingle().Which.CriterionToken.Should().Be("BV");
        result.Outputs.Should().ContainSingle().Which.IsPrimary.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenScoreBelongsToDifferentProject_ReturnsNull()
    {
        // Arrange
        var scoreId = Guid.NewGuid();
        var score = _scoreFaker.WithId(scoreId).WithProjectId(Guid.NewGuid()).Generate();
        _dbContext.AddProjectScore(score);

        // Act
        var result = await _handler.Handle(
            new GetProjectScoreQuery(Guid.NewGuid(), scoreId),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenScoreNotFound_ReturnsNull()
    {
        // Arrange & Act
        var result = await _handler.Handle(
            new GetProjectScoreQuery(Guid.NewGuid(), Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    public void Dispose() => _dbContext.Dispose();
}