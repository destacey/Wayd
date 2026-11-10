using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Application.Risks.Queries;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.Risks.Queries;

public sealed class GetTeamsOpenRiskCountQueryHandlerTests
{
    private readonly FakePlanningDbContext _dbContext = new();

    [Fact]
    public async Task Handle_CountsEachTeamsOpenRisks_LeavingOutTeamsWithNone()
    {
        // Arrange
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        var quiet = Guid.NewGuid();
        _dbContext.AddRisk(new RiskFaker().WithTeamId(teamA).Generate());
        _dbContext.AddRisk(new RiskFaker().WithTeamId(teamA).Generate());
        _dbContext.AddRisk(new RiskFaker().WithTeamId(teamA).WithStatus(RiskStatus.Closed).Generate());
        _dbContext.AddRisk(new RiskFaker().WithTeamId(teamB).Generate());
        _dbContext.AddRisk(new RiskFaker().WithTeamId(quiet).WithStatus(RiskStatus.Closed).Generate());
        _dbContext.AddRisk(new RiskFaker().WithTeamId(Guid.NewGuid()).Generate());
        var sut = new GetTeamsOpenRiskCountQueryHandler(_dbContext);

        // Act
        var result = await sut.Handle(new GetTeamsOpenRiskCountQuery([teamA, teamB, quiet]), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEquivalentTo(new Dictionary<Guid, int> { [teamA] = 2, [teamB] = 1 });
    }
}
