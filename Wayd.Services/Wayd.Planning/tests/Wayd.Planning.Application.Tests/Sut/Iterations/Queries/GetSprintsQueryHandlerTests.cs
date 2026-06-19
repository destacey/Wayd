using Wayd.Planning.Application.Iterations.Queries;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.Iterations.Queries;

public class GetSprintsQueryHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly GetSprintsQueryHandler _handler;
    private readonly IterationFaker _iterationFaker;

    public GetSprintsQueryHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _handler = new GetSprintsQueryHandler(_dbContext);
        _iterationFaker = new IterationFaker();
    }

    [Fact]
    public async Task Handle_ReturnsOnlySprints_WhenMixedIterationTypesExist()
    {
        // Arrange
        var sprint = _iterationFaker.AsSprint().Generate();
        var nonSprint = _iterationFaker.AsIteration().Generate();
        _dbContext.AddIterations([sprint, nonSprint]);

        // Act
        var result = await _handler.Handle(new GetSprintsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle(s => s.Id == sprint.Id);
        result.Should().NotContain(s => s.Id == nonSprint.Id);
    }

    [Fact]
    public async Task Handle_ExcludesSprints_WhenTeamIdIsNull()
    {
        // Arrange
        var sprintWithTeam = _iterationFaker.AsSprint().Generate(); // TeamId is random Guid by default
        var sprintWithoutTeam = _iterationFaker.AsSprint().WithTeamId(null).Generate();
        _dbContext.AddIterations([sprintWithTeam, sprintWithoutTeam]);

        // Act
        var result = await _handler.Handle(new GetSprintsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle(s => s.Id == sprintWithTeam.Id);
        result.Should().NotContain(s => s.Id == sprintWithoutTeam.Id);
    }

    [Fact]
    public async Task Handle_ReturnsOnlyTeamSprints_WhenTeamIdFilterProvided()
    {
        // Arrange
        var teamId = Guid.NewGuid();
        var teamSprint = _iterationFaker.AsSprint().WithTeamId(teamId).Generate();
        var otherSprint = _iterationFaker.AsSprint().WithTeamId(Guid.NewGuid()).Generate();
        _dbContext.AddIterations([teamSprint, otherSprint]);

        // Act
        var result = await _handler.Handle(new GetSprintsQuery(teamId), TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle(s => s.Id == teamSprint.Id);
        result.Should().NotContain(s => s.Id == otherSprint.Id);
    }

    [Fact]
    public async Task Handle_ReturnsEmpty_WhenNoSprintsExist()
    {
        // Act
        var result = await _handler.Handle(new GetSprintsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    public void Dispose() => _dbContext.Dispose();
}
