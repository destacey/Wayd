using FluentAssertions;
using Wayd.ProjectPortfolioManagement.Application.Projects.Scoring.Queries;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Scoring.Queries;

public class GetProjectScoresQueryHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly GetProjectScoresQueryHandler _handler;
    private readonly ProjectScoreFaker _scoreFaker = new();

    public GetProjectScoresQueryHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _handler = new GetProjectScoresQueryHandler(_dbContext);
    }

    [Fact]
    public async Task Handle_ReturnsScoresForProject_OrderedBySequenceDescending()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var first = _scoreFaker.WithProjectId(projectId).WithSequence(1).WithPrimaryValue(5m).Generate();
        var second = _scoreFaker.WithProjectId(projectId).WithSequence(2).WithPrimaryValue(9m).Generate();
        _dbContext.AddProjectScores([first, second]);

        // Act
        var result = await _handler.Handle(
            new GetProjectScoresQuery(projectId),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(2);
        result.Select(s => s.Sequence).Should().ContainInOrder(2L, 1L);
        result[0].PrimaryValue.Should().Be(9m);
    }

    [Fact]
    public async Task Handle_ExcludesScoresFromOtherProjects()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var mine = _scoreFaker.WithProjectId(projectId).WithSequence(1).Generate();
        var other = _scoreFaker.WithProjectId(Guid.NewGuid()).WithSequence(1).Generate();
        _dbContext.AddProjectScores([mine, other]);

        // Act
        var result = await _handler.Handle(
            new GetProjectScoresQuery(projectId),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle().Which.Id.Should().Be(mine.Id);
    }

    [Fact]
    public async Task Handle_WhenNoScores_ReturnsEmpty()
    {
        // Arrange & Act
        var result = await _handler.Handle(
            new GetProjectScoresQuery(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    public void Dispose() => _dbContext.Dispose();
}