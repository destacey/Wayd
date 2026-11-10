using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Work.Application.WorkItemDependencies.Dtos;
using Wayd.Work.Application.WorkItems.Queries;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// Runs against real SQL Server because the filter reaches each end's team and status through navigations, which
/// the fakes never translate.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetTeamsDependencyHealthQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;
    private readonly ForecastSeeder _seed = new(fixture);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<TeamsDependencyHealthDto> Health(params Guid[] teamIds)
    {
        await using var accessor = new WaydDbContextAccessor(_fixture);
        return await new GetTeamsDependencyHealthQueryHandler(accessor.Context).Handle(new GetTeamsDependencyHealthQuery(teamIds), Ct);
    }

    [Fact]
    public async Task Handle_CountsDependenciesWithinTheTeam_AsTheTeamPageDoes()
    {
        // Arrange
        await _seed.Reset(Ct);
        var team = await _seed.AddTeam(Ct);
        var outside = await _seed.AddTeam(Ct);
        var a1 = await _seed.AddItem(team.Id, Ct);
        var a2 = await _seed.AddItem(team.Id, Ct);
        var started = await _seed.AddItem(team.Id, Ct, StatusCategory.Active);
        var o1 = await _seed.AddItem(outside.Id, Ct);

        await _seed.AddDependency(a1.Id, a2.Id, Ct);            // within the team, both unplanned: at risk
        await _seed.AddDependency(o1.Id, started.Id, Ct);       // successor started first: unhealthy
        await _seed.AddDependency(a2.Id, o1.Id, Ct, removed: true);

        // Act
        var health = await Health(team.Id);

        // Assert
        health.Teams[team.Id].Should().Be(new TeamDependencyHealthDto { OpenCount = 2, AtRiskCount = 1, UnhealthyCount = 1 });
        health.AtRiskOrUnhealthyCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_LeavesOutDependenciesWhosePredecessorIsDone()
    {
        // Arrange
        await _seed.Reset(Ct);
        var team = await _seed.AddTeam(Ct);
        var done = await _seed.AddItem(team.Id, Ct, StatusCategory.Done);
        var open = await _seed.AddItem(team.Id, Ct);
        await _seed.AddDependency(done.Id, open.Id, Ct);

        // Act
        var health = await Health(team.Id);

        // Assert
        health.Teams.Should().BeEmpty();
        health.AtRiskOrUnhealthyCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_DependencyBetweenTwoOfTheTeams_CountsForEachButOnceInTheTotal()
    {
        // Arrange
        await _seed.Reset(Ct);
        var teamA = await _seed.AddTeam(Ct);
        var teamB = await _seed.AddTeam(Ct);
        var a = await _seed.AddItem(teamA.Id, Ct);
        var b = await _seed.AddItem(teamB.Id, Ct);
        await _seed.AddDependency(a.Id, b.Id, Ct);

        // Act
        var health = await Health(teamA.Id, teamB.Id);

        // Assert
        health.Teams[teamA.Id].AtRiskCount.Should().Be(1);
        health.Teams[teamB.Id].AtRiskCount.Should().Be(1);
        health.AtRiskOrUnhealthyCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_NoTeams_ReturnsNothing()
    {
        // Act
        var health = await Health();

        // Assert
        health.Teams.Should().BeEmpty();
    }
}
